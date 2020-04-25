{pkgs ? import <nixpkgs> {}}:

let pkg =
  { stdenv, lib, bash, callPackage, writeText, makeWrapper, writeScript, dotnet-sdk,
    patchelf, libunwind, coreclr, libuuid, curl, zlib, icu }:

  let
    nuget = callPackage ./nix/nuget.nix {};
    dotnet = callPackage ./nix/dotnet.nix {
      inherit nuget coreclr dotnet-sdk;
    };

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
  };

in
  pkgs.callPackage pkg { }
