{pkgs ? import (builtins.fetchTarball {
  name = "nixos-unstable-2020-04-25";
  url = "https://github.com/nixos/nixpkgs/archive/22a3bf9fb9edad917fb6cd1066d58b5e426ee975.tar.gz";
  sha256 = "089hqg2r2ar5piw9q5z3iv0qbmfjc4rl5wkx9z16aqnlras72zsa";
}) {} }:

let pkg =
  { stdenv, lib, bash, callPackage, writeText, makeWrapper, writeScript, dotnet-sdk,
    patchelf, libunwind, coreclr, libuuid, curl, zlib, icu }:

  let
    dotnet = callPackage ./dotnet-build.nix {};
  in dotnet.mkDotNetCoreProject {
    project = "dotnet2nix";
    version = "0.4";
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
