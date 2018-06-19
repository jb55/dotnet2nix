
namespace Dotnet2Nix.Util

module PathUtil =
  open System.Text
  open System


  //
  //   simpler method:
  //   instead of tree, just look at longest shared root path
  //

  let (&&&) f g = fun a -> (f a, g a)

  let uniqC xs =
       Seq.groupBy id xs
    |> Seq.map (snd >> (Seq.length &&& Seq.head))

  let public truncateLongRoots (paths:string seq) (limit:int) =
    let splitter (p:string) = p.Split('/')
    let joiner (p:string []) = String.Join("/", p)

    let splitPaths : string [] list = 
      Seq.map splitter paths 
        |> Seq.toList

    let longRoots : string list = 
      Seq.map Seq.head splitPaths
        |> uniqC
        |> Seq.filter (fst >> fun x -> x >= limit)
        |> Seq.map snd
        |> Seq.toList

    let hasLongRoot splitPath = 
      List.contains (Array.head splitPath) longRoots

    let pathShortener paths path =
      if hasLongRoot path
        then 
          let root = Array.head path
          if List.contains root paths
            then paths
            else root :: paths 
        else joiner path :: paths

    let newPaths =
      Seq.fold pathShortener [] splitPaths

    newPaths
