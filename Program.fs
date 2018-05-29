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
            |> List.filter (gets >> Option.map filterFile >> defaultRaise "file was not a string")

        let (objMap:Map<string, Json>) = 
            Map.ofList [ "baseName", Json.String name
                         "version", Json.String ver 
                         "sha512", Json.String sha512
                         "path", Json.String path
                         "files", Json.Array outputFiles
                       ]

        Json.Object objMap :: libs

    let usage =
        Console.WriteLine("dotnet2nix obj/project.assets.json [output-nuget-packages.json]")
        1

    [<EntryPoint>]
    let main argv =
        let input = Array.tryItem 0 argv
        let output = Array.tryItem 1 argv 
                       |> Option.fold (fun _ file -> file) "nuget-packages.json"
        match input with
          | None -> usage
          | Some filename ->
                let libs = loadLibraries filename
                let pkgs = Map.fold makePackage List.empty libs
                let serialized = Json.formatWith JsonFormattingOptions.Pretty (Json.Array pkgs)
                File.WriteAllText(output, serialized)
                0

