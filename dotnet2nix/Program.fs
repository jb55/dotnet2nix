﻿// Learn more about F# at http://fsharp.org

namespace Dotnet2Nix


module Application =

    open Chiron
    open System
    open System.IO
    open FSharp.Collections.ParallelSeq
    open Dotnet2Nix.Util
    open System.Diagnostics

    let rec combineProjectLibs libs key lib =
        let libPath =
            lazy
            match JsonUtil.strkey "path" lib with
                | Some s -> s
                | _ -> raise (new Exception("No path found in project"))

        match JsonUtil.strkey "type" lib with
            | Some "project" ->
                let otherProj = Path.GetDirectoryName(libPath.Force())
                try
                    let projectLibs = loadLibraries otherProj
                    let libs_ = Map.fold (fun o k v -> Map.add k v o) libs projectLibs
                    Map.remove key libs_
                with
                    | _ -> Map.remove key libs
            | _ -> libs

    and loadLibraries (path:string) =
        let file = Path.Combine(path, "obj/project.assets.json")
        let data = File.ReadAllText(file)
        let json = Json.parse data

        let libs =
            match JsonUtil.getkey "libraries" json with
              | Some (Json.Object o) -> o
              | _ -> raise (new Exception("Library json was not an object"))

        Map.fold combineProjectLibs libs libs

    let fold1 f (x::xs) = List.fold f x xs

    let loadAssets (files:string list) =
        fold1 (Map.fold (fun o k v -> Map.add k v o)) (List.map loadLibraries files)

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

    let rec makePackage ((libName,lib):string * Json) =

        let (name, ver) =
          match libName.Split('/') with
            | [| name; ver |] -> (name, ver)
            | _ -> raise (new Exception("name and version not found in library name"))

        let pathName = sprintf "%s-%s.zip" name ver
        let url    = sprintf "https://www.nuget.org/api/v2/package/%s/%s" name ver
        let sha256 = nixPrefetchUrl url pathName
        let path   = JsonUtil.getkeyf "path" lib JsonUtil.gets

        let sha512 =
          JsonUtil.getkeyf "sha512" lib JsonUtil.gets
            |> Convert.FromBase64String
            |> JsonUtil.byteHexStr

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
                         "sha512", Json.String sha512
                         "path", Json.String path
                         "outputFiles", Json.Array (List.map Json.String truncatedOutputFiles)
                       ]

        Json.Object objMap

    let usage = lazy let _ = Console.WriteLine("dotnet2nix [Project..]")
                     1

    [<EntryPoint>]
    let main argv =
        let output = "nuget-packages.json"

        if argv.Length = 0
          then usage.Force()
          else
            let files      = Array.toList argv
            let libs       = loadAssets files
            let pkgs       = PSeq.map makePackage (Map.toSeq libs) |> PSeq.toList
            let serialized = Json.formatWith JsonFormattingOptions.Pretty (Json.Array pkgs)
            Console.Error.WriteLine(String.Format("writing to {0}", output))
            File.WriteAllText(output, serialized)
            0
