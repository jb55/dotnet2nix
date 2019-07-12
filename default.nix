{ stdenv, lib, bash, callPackage, writeText, makeWrapper, writeScript, dotnet-sdk,
  patchelf, libunwind, coreclr, libuuid, curl, zlib, icu }:

let
  env = ''
    tmp="$(mktemp -d)"
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export HOME="$tmp"
  '';

  dotnetBuildPhase = { path, target, pkgset, config, project }: ''
    ${env}
    pushd ${path}
    dotnet publish -r ${target} --source ${pkgset} -c ${config} ${project}
    popd
  '';

  nuget = callPackage ./nix/nuget.nix {};
  dotnet = callPackage ./nix/dotnet.nix { inherit nuget coreclr dotnet-sdk; };

in dotnet.mkDotNetCoreProject {
  project = "dotnet2nix";
  version = "1.0.0";
  config = "Release";

  src = ./.;

  meta = with stdenv.lib; {
    description = "Generate nix expressions for dotnet core projects";
    homepage = "https://github.com/jb55/dotnet2nix";
    license = licenses.mit;
    maintainers = [ maintainers.jb55 ];
    platforms = with platforms; linux;
  };
}
