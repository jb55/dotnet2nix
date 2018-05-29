// Learn more about F# at http://fsharp.org

open System

open Chiron
open System.IO

let rec loadLibraries file =
    let data = File.ReadAllText(file)
    let json = Json.parse data
    let libs =
        match json with
          | Json.Object o -> o
          | _ -> raise (new Exception("Library json was not an object"))
    let keys = libs |> Map.toSeq |> Seq.map fst
    let collectLibs libObj key =
        let lib = Map.find key libs
        let libPath = 
          lazy 
          match lib with
            | Property "path" (Json.String s) -> s
            | _ -> raise (new Exception("No path found in project"))
        match lib with
          | Property "type" (Json.String "project") ->
              let otherProj = Path.GetDirectoryName(libPath.Force())
              try 
                  let otherAssets = Path.Combine(otherProj, "obj/project.assets.json")
                  let otherJson = loadLibraries otherAssets
                  Map.fold (fun o k v -> Map.add k v o) libObj otherJson
              with 
                  | _ -> libObj
          | _ -> Map.add key lib libObj
    Seq.fold collectLibs Map.empty keys


[<EntryPoint>]
let main argv =
    let libs = loadLibraries "test.json"
    printfn "%s\n" (libs.ToString())
    0 // return an integer exit code

