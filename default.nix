{ stdenv, lib, bash, callPackage, writeText, makeWrapper, writeScript, dotnet-sdk,
  patchelf, libunwind, coreclr, libuuid, curl, zlib, icu }:

let
  mkDotNetCoreProject = attrs@{ version, project, config ? "Release", target ? "linux-x64", ... }:
    let
      rpath = stdenv.lib.makeLibraryPath [ libunwind coreclr libuuid stdenv.cc.cc curl zlib icu ];

      nuget-pkg-json = lib.importJSON (./. + "/${project}/nuget-packages.json");
      #other-pkg-json = lib.importJSON "${project}"./other-packages.json;

      fetchNuGet = callPackage ./nix/fetchnuget {};

      make-nuget-pkg = pkgjson: {
        package = fetchNuGet pkgjson;
        meta = pkgjson;
      };

      make-nuget-pkgset = callPackage ./nix/make-nuget-packageset {};

      nuget-pkgs    = map make-nuget-pkg nuget-pkg-json;
      nuget-pkg-dir = make-nuget-pkgset "${project}-nuget-pkgs" nuget-pkgs;

      nuget-config = writeText "nuget.config" ''
        <configuration>
        <packageSources>
            <clear />
            <add key="local" value="${nuget-pkg-dir}" />
        </packageSources>
        </configuration>
      '';

      runtime-config = writeText "runtimeconfig.json" ''
        {
          "runtimeOptions": {
            "additionalProbingPaths": [
              "${nuget-pkg-dir}"
            ]
          }
        }
      '';
    in
    stdenv.mkDerivation (rec {
      baseName = "${project}";
      inherit version;
      name = "${baseName}-${config}-${version}";

      src = lib.sourceFilesBySuffices ./. [
        ".cs"
        ".fs"
        ".xml"
        ".props"
        ".projitems"
        ".config"
        ".csproj"
        ".fsproj"
        "appsettings.${config}.json"
        "appsettings.json"
        ".html"
        ".sln"
      ];

      buildInputs = [ dotnet-sdk makeWrapper patchelf ];

      configurePhase = ''
        cp ${nuget-config} nuget.config
      '';

      patchPhase = ''
        # shouldn't need tools
        sed -i '/DotNetCliToolReference/d' ${project}/${project}.csproj || :
        sed -i '/DotNetCliToolReference/d' ${project}/${project}.fsproj || :
      '';

      buildPhase = ''
        tmp="$(mktemp -d)"
        export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
        export DOTNET_CLI_TELEMETRY_OPTOUT=1
        export HOME="$tmp"
        dotnet publish -r ${target} --source ${nuget-pkg-dir} -c ${config} ${project}
      '';


      installPhase = ''
        mkdir -p $out
        cp -r ${project}/bin/${config}/netcoreapp2.0/${target}/publish $out/bin
        cp ${runtime-config} $out/bin/${project}.runtimeconfig.json

        patchelf --set-interpreter "$(cat $NIX_CC/nix-support/dynamic-linker)" "$out/bin/${project}" || :
        patchelf --set-rpath "${rpath}" "$out/bin/${project}" || :

        wrapProgram "$out/bin/${project}" \
          --prefix LD_LIBRARY_PATH : ${rpath}

        find $out/bin -type f -name "*.so" -exec patchelf --set-rpath "${rpath}" {} \;

        # need this because managed dlls get upset if the name is different
        mv $out/bin/${project} $out/bin/${project}-${config}
        sed -i s/.${project}-wrapped/${project}/g $out/bin/${project}-${config}
        mv $out/bin/.${project}-wrapped $out/bin/${project}
      '';

      dontStrip = true;

      meta = with stdenv.lib; {
        description = "Generate nix expressions for dotnet core projects";
        homepage = "https://github.com/jb55/dotnet2nix";
        license = licenses.mit;
        maintainers = [ maintainers.jb55 ];
        platforms = with platforms; linux;
      };
    } // attrs);

in mkDotNetCoreProject {
  project = "dotnet2nix";
  version = "1.0.0";
  config = "Release";
}
