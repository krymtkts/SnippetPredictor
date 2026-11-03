namespace SnippetPredictorBenchmark

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Threading
open BenchmarkDotNet.Attributes
open SnippetPredictor

type HistoryScenario =
    | Empty = 0
    | Small = 1
    | Medium = 2
    | Unique = 3
    | Long = 4
    | Duplicates = 5

type HistoryPosition =
    | First = 0
    | Last = 1
    | Missing = 2

module HistoryData =
    let texts scenario =
        let count, length, unique =
            match scenario with
            | HistoryScenario.Empty -> 0, 32, 0
            | HistoryScenario.Small -> 10, 32, 10
            | HistoryScenario.Medium -> 1000, 32, 1000
            | HistoryScenario.Long -> 1000, 4096, 1000
            | HistoryScenario.Duplicates -> 10000, 32, 1000
            | _ -> 10000, 32, 10000

        Array.init count (fun index ->
            let suffix = (index % unique).ToString("D8")
            String('x', length - suffix.Length) + suffix)

    let index (capacity: int) (texts: string array) =
        let lookup = HashSet<string>(capacity, StringComparer.Ordinal)

        for text in texts do
            lookup.Add text |> ignore

        lookup

    let rec contains (texts: string array) query position =
        position < texts.Length
        && (String.Equals(texts[position], query, StringComparison.Ordinal)
            || contains texts query (position + 1))

type HistoryFixture(scenario: HistoryScenario, position: HistoryPosition) =
    let directory =
        Directory.CreateTempSubdirectory("SnippetPredictor.HistoryBenchmark.")

    let path = Path.Combine(directory.FullName, Config.snippetFilesName)
    let cache = new Suggestion.Cache()
    let mutable texts = Array.empty<string>
    let mutable query = ""

    let dispose () =
        (cache :> IDisposable).Dispose()
        directory.Delete(true)

    do
        try
            let entries =
                HistoryData.texts scenario |> Array.map (fun text -> {| Snippet = text |})

            File.WriteAllText(path, JsonSerializer.Serialize({| Snippets = entries |}))
            cache.load (fun () -> directory.FullName, path)
            let stopwatch = Stopwatch.StartNew()

            while not (cache.isUnknownGroupIdentifier ":unsupported")
                  && stopwatch.ElapsedMilliseconds < 10000L do
                Thread.Sleep 20

            if not (cache.isUnknownGroupIdentifier ":unsupported") then
                invalidOp "History benchmark configuration did not finish loading."

            texts <- cache.getCompletionTexts ":snp"

            if texts.Length <> entries.Length then
                invalidOp "History benchmark returned an unexpected number of snippets."

            let text =
                if texts.Length = 0 || position = HistoryPosition.Missing then
                    "missing"
                elif position = HistoryPosition.First then
                    texts[0]
                else
                    texts[texts.Length - 1]

            query <- String(text.ToCharArray())

            if cache.isRegisteredSnippet query <> HistoryData.contains texts query 0 then
                invalidOp "History lookup disagrees with the array baseline."
        with _ ->
            dispose ()
            reraise ()

    member _.Indexed() = cache.isRegisteredSnippet query
    member _.Scanned() = HistoryData.contains texts query 0

    interface IDisposable with
        member _.Dispose() = dispose ()

[<MemoryDiagnoser>]
type HistoryLookupBenchmarks() =
    let mutable fixture: HistoryFixture option = None

    let current () =
        match fixture with
        | Some value -> value
        | None -> invalidOp "History fixture has not been initialized."

    [<ParamsAllValues>]
    member val Scenario = HistoryScenario.Small with get, set

    [<ParamsAllValues>]
    member val Position = HistoryPosition.First with get, set

    [<GlobalSetup>]
    member this.Setup() =
        fixture <- Some(new HistoryFixture(this.Scenario, this.Position))

    [<GlobalCleanup>]
    member _.Cleanup() =
        fixture |> Option.iter (fun value -> (value :> IDisposable).Dispose())
        fixture <- None

    [<Benchmark(Baseline = true)>]
    member _.ArrayScan() = (current ()).Scanned()

    [<Benchmark>]
    member _.Indexed() = (current ()).Indexed()

[<MemoryDiagnoser>]
type HistoryIndexBenchmarks() =
    let mutable texts = Array.empty<string>

    [<ParamsAllValues>]
    member val Scenario = HistoryScenario.Small with get, set

    [<GlobalSetup>]
    member this.Setup() =
        texts <- HistoryData.texts this.Scenario
        let lookup = HistoryData.index 0 texts

        for text in texts do
            let found, stored = lookup.TryGetValue text

            if
                not found
                || not (Object.ReferenceEquals(texts |> Array.find (fun candidate -> candidate = text), stored))
            then
                invalidOp "The index must share an existing string instance."

    [<Benchmark(Baseline = true)>]
    member _.Growing() = HistoryData.index 0 texts

    [<Benchmark>]
    member _.Presized() = HistoryData.index texts.Length texts
