// Learn more about F# at http://fsharp.org

namespace Dotnet2Nix


module Application =

    open Chiron
    open System
    open System.IO
    open Dotnet2Nix.Util
    open System.Diagnostics


    let rec loadLibraries (file:string) =
        let data = File.ReadAllText(file)
        let json = Json.parse data

        let libs =
            match JsonUtil.getkey "libraries" json with
              | Some (Json.Object o) -> o
              | _ -> raise (new Exception("Library json was not an object"))

        let combineProjectLibs libs key lib =
            let libPath =
              lazy
              match JsonUtil.strkey "path" lib with
                | Some s -> s
                | _ -> raise (new Exception("No path found in project"))

            match JsonUtil.strkey "type" lib with
              | Some "project" ->
                  let otherProj = Path.GetDirectoryName(libPath.Force())
                  try
                      let projectAssets = Path.Combine(otherProj, "obj/project.assets.json")
                      let projectLibs = loadLibraries projectAssets
                      let libs_ = Map.fold (fun o k v -> Map.add k v o) libs projectLibs
                      Map.remove key libs_
                  with
                      | _ -> Map.remove key libs
              | _ -> libs

        Map.fold combineProjectLibs libs libs

    let nixPrefetchUrl (url:string) (name:string) =
        let args = sprintf "--name %s %s" name url
        let processStartInfo =
          ProcessStartInfo(
              FileName = "nix-prefetch-url",
              Arguments = args,
              RedirectStandardOutput = true,
              UseShellExecute = false
          )
        let proc = Process.Start(processStartInfo)
        let output = proc.StandardOutput.ReadToEnd()
        proc.WaitForExit()
        output.Trim()


    let rec makePackage (libs:Json list) (libName:string) (lib:Json) =
        let (name, ver) =
          match libName.Split('/') with
            | [| name; ver |] -> (name, ver)
            | _ -> raise (new Exception("name and version not found in library name"))

        let pathName = sprintf "%s-%s.zip" name ver
        let url    = sprintf "https://www.nuget.org/api/v2/package/%s/%s" name ver
        let sha256 = nixPrefetchUrl url pathName
        let path   = JsonUtil.getkeyf "path" lib JsonUtil.gets

        let filterFile (file:string) =
          not (file.EndsWith(".nuspec") || file.EndsWith(".txt"))

        // TODO: filter long
        let outputFiles =
          JsonUtil.getkeyf "files" lib JsonUtil.geta
            |> List.map (JsonUtil.gets >> JsonUtil.defaultRaise "file was not a string")
            |> List.filter filterFile

        // need to do this because of bash: argument list too long
        let truncatedOutputFiles =
          PathUtil.truncateLongRoots outputFiles 30

        let (objMap:Map<string, Json>) =
            Map.ofList [ "baseName", Json.String name
                         "version", Json.String ver
                         "sha256", Json.String sha256
                         "path", Json.String path
                         "outputFiles", Json.Array (List.map Json.String truncatedOutputFiles)
                       ]

        Json.Object objMap :: libs

    let usage = lazy let _ = Console.WriteLine("dotnet2nix Some.Project/obj/project.assets.json [output-nuget-packages.json]")
                     1

    [<EntryPoint>]
    let main argv =

        let input =
          Array.tryItem 0 argv
            |> Option.fold (fun _ f -> f) (Path.Combine("obj", "project.assets.json"))

        let output =
          Array.tryItem 1 argv
            |> Option.fold (fun _ file -> file) "nuget-packages.json"

        let inputExists = File.Exists(input)

        if not inputExists
          then usage.Force()
          else
            let libs = loadLibraries input
            let pkgs = Map.fold makePackage List.empty libs
            let serialized = Json.formatWith JsonFormattingOptions.Pretty (Json.Array pkgs)
            Console.Error.WriteLine(String.Format("writing to {0}", output))
            File.WriteAllText(output, serialized)
            0
