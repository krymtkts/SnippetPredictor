module SnippetPredictorTest.Utility

open System
open System.IO

let normalizeNewlines (s: string) = s.Replace("\r\n", "\n")

type TempDirectory(directory: string) =
    member val Path: string = Directory.CreateTempSubdirectory(directory).FullName

    interface IDisposable with
        member __.Dispose() =
            if Directory.Exists(__.Path) then
                Directory.Delete(__.Path, true)
            else
                failwith $"Directory '{__.Path}' does not exist. maybe the test failed to create it."
