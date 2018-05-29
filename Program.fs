// Learn more about F# at http://fsharp.org

namespace Dotnet2Nix

module Application =

    open Chiron
    open System
    open System.IO
    open Dotnet2Nix.Util.JsonUtil

    let rec loadLibraries (file:string) =
        let data = File.ReadAllText(file)
        let json = Json.parse data

        let libs =
            match getkey "libraries" json with
              | Some (Json.Object o) -> o
              | _ -> raise (new Exception("Library json was not an object"))

        let combineProjectLibs libs _ lib =
            let libPath = 
              lazy 
              match strkey "path" lib with
                | Some s -> s
                | _ -> raise (new Exception("No path found in project"))

            match strkey "type" lib with
              | Some "project" ->
                  let otherProj = Path.GetDirectoryName(libPath.Force())
                  try 
                      let projectAssets = Path.Combine(otherProj, "obj/project.assets.json")
                      let projectLibs = loadLibraries projectAssets
                      Map.fold (fun o k v -> Map.add k v o) libs projectLibs
                  with 
                      | _ -> libs
              | _ -> libs

        Map.fold combineProjectLibs libs libs


    let makePackage (libs:Json list) (libName:string) (lib:Json) =
        let (name, ver) =
          match libName.Split('/') with
            | [| name; ver |] -> (name, ver)
            | _ -> raise (new Exception("name and version not found in library name"))

        let sha512 = 
          getkeyf "sha512" lib gets
            |> Convert.FromBase64String 
            |> byteHexStr

        let path = 
          getkeyf "path" lib gets

        let filterFile (file:string) =
          not (file.EndsWith(".nuspec") || file.EndsWith(".txt"))

        let outputFiles = 
          getkeyf "files" lib geta
            |> List.map (gets >> defaultRaise "files element was not a string")
            |> List.filter filterFile

        let (objMap:Map<string, Json>) = 
            Map.ofList [ "baseName", Json.String name
                         "version", Json.String ver 
                         "sha512", Json.String sha512
                         "path", Json.String path
                         "files", Json.Array (List.map Json.String outputFiles)
                       ]

        Json.Object objMap :: libs


    [<EntryPoint>]
    let main argv =
        let libs = loadLibraries "..\..\..\obj\project.assets.json"
        let pkgs = Map.fold makePackage List.empty libs
        let serialized = Json.formatWith JsonFormattingOptions.Pretty (Json.Array pkgs)
        Console.Write(serialized);

        0 // return an integer exit code

