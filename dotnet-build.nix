{ lib, stdenv, dotnet-sdk, makeWrapper, patchelf, libunwind, coreclr, writeText, fetchurl
, libuuid, stdenvNoCC, xxd, unzip, curl, zlib, openssl, icu, writeScript, bash, callPackage, lndir, runCommand }:
rec {
  rpath = stdenv.lib.makeLibraryPath [ libunwind coreclr libuuid openssl stdenv.cc.cc curl zlib icu ];

  mkNugetPkgSet_ = name: pkgs:
    let
      args = {
        preferLocalBuild = true;
        allowSubstitutes = false;
        inherit name;
      };

      make-cmd = pkg: ''
        mkdir -p $out/${pkg.meta.path}
        ${lndir}/bin/lndir -silent "${pkg.package}" "$out/${pkg.meta.path}"
      '';

    in runCommand name args
      ''
        ${lib.strings.concatStringsSep "\n" (map make-cmd pkgs)}
      '';

  fetchNuGet =
    attrs @
    { baseName
    , version
    , outputFiles
    , url ? "https://www.nuget.org/api/v2/package/${baseName}/${version}"
    , sha256 ? ""
    , sha512
    , md5 ? ""
    , ...
    }:
    if md5 != "" then
      throw "fetchnuget does not support md5 anymore, please use sha256"
    else
      let
        arrayToShell = (a: toString (map (lib.escape (lib.stringToCharacters "\\ ';$`()|<>\t") ) a));

        make-cp = outFile: ''
          outFile="${outFile}"
          [[ ''${outFile: -7} == ".sha512" ]] && echo -n "${sha512}" \
            | ${lib.getBin xxd}/bin/xxd -r -p \
            | base64 -w500 > ${outFile}
          cp -r --parents -t $out "${outFile}" || :
        '';

        nupkg-name = lib.strings.toLower "${baseName}.${version}.nupkg";
      in
      stdenvNoCC.mkDerivation ({
        name = "${baseName}-${version}";

        src = fetchurl {
          inherit url sha256;
          name = "${baseName}.${version}.zip";
        };

        sourceRoot = ".";

        buildInputs = [ unzip ];

        dontStrip = true;

        installPhase = ''
          mkdir -p $out
          chmod +r *.nuspec
          cp *.nuspec $out
          cp $src $out/${nupkg-name}
          ${lib.strings.concatStringsSep "\n" (map make-cp outputFiles)}
        '';

        # not sure if this necessary
        preInstall = ''
          # function traverseRename () {
          #   for e in *
          #   do
          #     t="$(echo "$e" | sed -e "s/%20/\ /g" -e "s/%2B/+/g")"
          #     [ "$t" != "$e" ] && mv -vn "$e" "$t"
          #     if [ -d "$t" ]
          #     then
          #       cd "$t"
          #       traverseRename
          #       cd ..
          #     fi
          #   done
          # }

          # traverseRename
      '';
      } // attrs);

  mkNugetPkg = pkgjson: {
    package = fetchNuGet pkgjson;
    meta = pkgjson;
  };

  mkNugetConfig = project: nuget-pkgset: writeText "${project}-nuget.config" ''
    <configuration>
    <packageSources>
        <clear />
        <add key="local" value="${nuget-pkgset}" />
    </packageSources>
    </configuration>
  '';

  mkRuntimeConfig = nuget-pkgset: writeText "runtimeconfig.json" ''
    {
      "runtimeOptions": {
        "additionalProbingPaths": [
          "${nuget-pkgset}"
        ]
      }
    }
  '';

  mkNuGetPkgs  = nugetPkgJson: map mkNugetPkg nugetPkgJson;
  mkNugetPkgSet = nugetPkgs: mkNugetPkgSet_ "nuget-pkgs" nugetPkgs;
  mkNugetPkgSetFromJson = json: mkNugetPkgSet (mkNuGetPkgs json);

  env = ''
    tmp="$(mktemp -d)"
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export HOME="$tmp"
  '';

  dotnetBuildPhase = buildPhase;

  buildPhase = { path, target, pkgset, config, project }: ''
    ${env}
    pushd ${path}
    mkdir -p $out/libexec/${project}
    dotnet publish -r ${target} --source ${pkgset} -c ${config} ${project} -o $out/libexec/${project}
    popd
  '';

mkDotNetCoreProject = attrs@{
    testProjects ? []
  , project
  , src
  , meta
  , installExtra ? ""
  , path ? "./"
  , config ? "Release"
  , target ? "linux-x64", ... }:
    let
      nuget-pkg-json = lib.importJSON (lesrc + "/${path}${project}/nuget-packages.json");
      nuget-pkgset   = mkNugetPkgSetFromJson nuget-pkg-json;
      nuget-config   = mkNugetConfig project nuget-pkgset;
      runtime-config = mkRuntimeConfig nuget-pkgset;

      bin-script = writeScript project ''
        #!${bash}/bin/bash
        exec ${dotnet-sdk}/bin/dotnet exec @@DEST@@/libexec/${project}/${project}.dll "$@"
      '';

      lesrc = src;
    in
    stdenv.mkDerivation (attrs // (rec {
      baseName = "${project}";
      pname = "${baseName}";
      version = attrs.version;

      buildInputs = [ dotnet-sdk makeWrapper patchelf ];

      inherit src;

      buildPhase = dotnetBuildPhase {
        inherit path target config project;
        pkgset = nuget-pkgset;
      };

      doCheck = lib.length testProjects > 0;

      checkPhase = ''
        ${lib.concatStringsSep "\n" (map (testProject:
        let
          testProjectPkgJson = lib.importJSON (lesrc + "/${testProject}/nuget-packages.json");
          testPkgset         = mkNugetPkgSetFromJson testProjectPkgJson;
          testNugetConfig    = mkNugetConfig (builtins.baseNameOf testProject) testPkgset;
        in ''
          cp ${testNugetConfig} nuget.config
          dotnet test ${testProject}
          rm -f nuget.config
        '') testProjects)}
      '';

      patchPhase =
        let proj = "${path}${project}/${project}";
        in ''
        # shouldn't need tools
        sed -i '/DotNetCliToolReference/d' ${proj}.csproj || :
        sed -i '/DotNetCliToolReference/d' ${proj}.csproj || :
      '';

      installPhase = ''
        mkdir -p $out/libexec/${project} $out/bin
        cd ${path}
        cp ${runtime-config} $out/bin/${project}.runtimeconfig.json

        if [ -f "$out/libexec/${project}/${project}" ]
        then
          <${bin-script} sed "s,@@DEST@@,$out," > $out/bin/${project}
          chmod +x $out/bin/${project}

          wrapProgram "$out/bin/${project}" --prefix LD_LIBRARY_PATH : ${rpath}
        fi

        find $out/bin -type f -name "*.so" -exec patchelf --set-rpath "${rpath}" {} \;

        ${installExtra}
      '';

      dontStrip = true;

      inherit meta;
    }));
}
