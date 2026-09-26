module SnippetPredictorTest.Utility

open System
open System.Diagnostics
open System.IO
open Expecto.Flip

// Resolve test data from the source directory so test execution doesn't depend on the runner's working directory.
let testAssetDirectory = __SOURCE_DIRECTORY__

let testAssetPath (fileName: string) =
    Path.Combine(testAssetDirectory, fileName)

let normalizeNewlines (s: string) = s.Replace("\r\n", "\n")

let waitUntil (timeoutMs: int) (pollMs: int) (predicate: unit -> bool) =
    let stopwatch = Stopwatch.StartNew()
    let mutable satisfied = predicate ()

    while not satisfied && stopwatch.ElapsedMilliseconds < int64 timeoutMs do
        Threading.Thread.Sleep pollMs
        satisfied <- predicate ()

    satisfied

let expectEventually message predicate =
    predicate |> waitUntil 5000 20 |> Expect.isTrue message

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

    member __.GetSnippetDirectoryPath() = directory.Path

    member __.GetSnippetPath() = path

    member __.GetSnippetContent() =
        File.ReadAllText(path) |> normalizeNewlines

type EnvironmentVariable(value: string) =
    static let gate = new Threading.SemaphoreSlim(1, 1)
    let name = "SNIPPET_PREDICTOR_CONFIG"
    let mutable originalValue = None

    do
        gate.Wait()

        try
            originalValue <- Environment.GetEnvironmentVariable(name) |> Option.ofObj
            Environment.SetEnvironmentVariable(name, value)
        with _ ->
            gate.Release() |> ignore
            reraise ()

    interface IDisposable with
        member __.Dispose() =
            try
                match originalValue with
                | None -> Environment.SetEnvironmentVariable(name, null)
                | Some value -> Environment.SetEnvironmentVariable(name, value)
            finally
                gate.Release() |> ignore
