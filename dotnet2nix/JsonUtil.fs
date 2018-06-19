

namespace Dotnet2Nix.Util

open Chiron
open System
open System.Text

module JsonUtil =
    let gets = 
      function
        | Json.String s -> Some s
        | _ -> None

    let geta = 
      function
        | Json.Array a -> Some a
        | _ -> None

    let defk (key:string) v = 
      let ex () = raise (new Exception(String.Format("'{0}' key not found", key)))
      Option.defaultWith ex v

    let defaultRaise msg =
      function
        | Some a -> a
        | None -> raise (new Exception(msg))

    let getkey key =
      function 
        | Json.Object o -> Map.tryFind key o
        | _ -> None

    let strkey key json =
      getkey key json |> Option.bind gets

    let getkeyf s lib jsonCons = 
      getkey s lib |> Option.bind jsonCons |> defk s

    let addHexByte (strBuilder:StringBuilder) (b:byte) =
        strBuilder.Append(String.Format("{0:x2}", b)) |> ignore
        strBuilder

    let byteHexStr (bs:byte[]) = 
        (Array.fold addHexByte (new StringBuilder()) bs).ToString()

