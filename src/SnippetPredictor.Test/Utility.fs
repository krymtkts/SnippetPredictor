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

type TempFile(fileName: string, content: string) =
    let directory = new TempDirectory("SnippetPredictor.Test.")
    let path = Path.Combine(directory.Path, fileName)

    do File.WriteAllText(path, content)

    interface IDisposable with
        member __.Dispose() = (directory :> IDisposable).Dispose()

    member __.GetSnippetPath() = directory.Path, path

    member __.GetSnippetContent() =
        File.ReadAllText(path) |> normalizeNewlines

type EnvironmentVariable(value: string) =
    let name = "SNIPPET_PREDICTOR_CONFIG"
    let originalValue = Environment.GetEnvironmentVariable(name)

    do Environment.SetEnvironmentVariable(name, value)

    interface IDisposable with
        member __.Dispose() =
            match originalValue with
            | null -> Environment.SetEnvironmentVariable(name, null)
            | _ -> Environment.SetEnvironmentVariable(name, originalValue)
