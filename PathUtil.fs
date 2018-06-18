
namespace Dotnet2Nix.Util

module PathUtil =


  //
  //   simpler method:
  //   instead of tree, just look at longest shared root path
  //

  //type DirTree<'a> when 'a : comparison =
  //  | Node of Map<'a, DirTree<'a>>
 
  //let dtNodes = function
  //  | Node nodes -> nodes

  //let rec countDepth (dt:DirTree<'a>) =
  //  let folder count (_, node) = count + countDepth node
  //  in Seq.fold folder 0 (Map.toSeq (dtNodes dt))

  //let getDepths = function
  //  | Node nodes -> let seqNodes = Map.toSeq nodes |> Seq.map snd
  //                  let counts = Seq.map countDepth seqNodes
  //                  in Seq.zip counts seqNodes

  //let toSimplify (xs:(int * DirTree<'a>) seq) limit =
  //  let folder nodes (count, node) = 
  //    if count >= limit then node :: nodes else nodes
  //  in Seq.fold folder [] xs

  //let pathsToDirTree (paths : string list) =
  //  let folder acc path =



  let pathToDirTree (path:string) =
    let splitPath = Array.rev (path.Split("/"))
    splitPath
  //  match splitPath with
  //    | [||]  -> Node Map.empty
  //    | [|x|] -> Node (Map.add x (Node Map.empty) Map.empty)
  //    | xs    -> Array.map 
  //
  //let longestCommonPrefix (paths:string seq) =
  //  Seq.map pathToDirTree
