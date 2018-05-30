
namespace Dotnet2Nix.Util

module PathUtil =

  type DirTree =
    | Folder of string * DirTree
    | File of string

  let pathToDirTree (path:string) =
    let splitPath = Array.rev (path.Split("/"))
    match splitPath with
      [||] -> None
      x :: xs -> 
  
  let longestCommonPrefix (paths:string seq) =