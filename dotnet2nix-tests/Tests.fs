namespace Dotnet2NixTests

open System
open Microsoft.VisualStudio.TestTools.UnitTesting
open Dotnet2Nix.Util

[<TestClass>]
type PathUtilTests () =

    [<TestMethod>]
    member this.PathTruncateWorks () =
        let paths    = ["a"; "a/b"; "a/c"; "b"; "d"]
        let expected = ["d"; "b"; "a"]
        let truncated = PathUtil.truncateLongRoots paths 3
        Assert.AreEqual(expected, truncated)
