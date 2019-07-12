{ lib, stdenv, nuget, dotnet-sdk, makeWrapper, patchelf, libunwind, coreclr
, libuuid, curl, zlib, openssl, icu }:
rec {

  rpath = stdenv.lib.makeLibraryPath [ libunwind coreclr libuuid openssl stdenv.cc.cc curl zlib icu ];

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
    dotnet publish -r ${target} --source ${pkgset} -c ${config} ${project}
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
      nuget-pkgset   = nuget.mkNugetPkgSetFromJson nuget-pkg-json;
      nuget-config   = nuget.mkNugetConfig project nuget-pkgset;
      runtime-config = nuget.mkRuntimeConfig nuget-pkgset;
      netCoreMajorVersion = "2.2";
      lesrc = src;
    in
    stdenv.mkDerivation (attrs // (rec {
      baseName = "${project}";
      name = "${baseName}-${config}-${attrs.version}";

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
          testPkgset         = nuget.mkNugetPkgSetFromJson testProjectPkgJson;
          testNugetConfig    = nuget.mkNugetConfig (builtins.baseNameOf testProject) testPkgset;
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
        find . -name '*.fsproj' -exec \
          sed -i '/<TargetFramework>/a <RuntimeFrameworkVersion>${dotnet-sdk.netCoreVersion}</RuntimeFrameworkVersion>' {} \;
      '';

      installPhase = ''
        mkdir -p $out/share
        cd ${path}
        cp -r ${project}/bin/${config}/netcoreapp${netCoreMajorVersion}/${target}/publish $out/bin
        cp ${runtime-config} $out/bin/${project}.runtimeconfig.json
        cp -r ${project}/Properties $out/bin || :


        if [ -f "$out/bin/${project}" ]
        then
          patchelf --set-interpreter "$(cat $NIX_CC/nix-support/dynamic-linker)" "$out/bin/${project}" || :
          patchelf --set-rpath "${rpath}" "$out/bin/${project}" || :

          wrapProgram "$out/bin/${project}" \
            --prefix LD_LIBRARY_PATH : ${rpath}

          # need this because managed dlls get upset if the name is different
          mv $out/bin/${project} $out/bin/${project}-${config}
          sed -i s/.${project}-wrapped/${project}/g $out/bin/${project}-${config}
          mv $out/bin/.${project}-wrapped $out/bin/${project}
        fi

        find $out/bin -type f -name "*.so" -exec patchelf --set-rpath "${rpath}" {} \;

        ${installExtra}
      '';

      dontStrip = true;

      inherit meta;
    }));
}
