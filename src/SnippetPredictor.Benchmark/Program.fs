namespace SnippetPredictorBenchmark

open System
open System.Diagnostics
open System.IO
open System.Management.Automation.Subsystem.Prediction
open System.Text.Json
open System.Threading
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open SnippetPredictor

type Scenario =
    | Mixed = 0
    | NoMatch = 1
    | Long = 2
    | Repeated = 3
    | Unicode = 4
    | EmptyQuery = 5
    | Snp = 6
    | Group = 7
    | Tooltip = 8
    | Sparse = 9
    | InternalTail = 10

type Fixture(count: int, scenario: Scenario) =
    let directory = Directory.CreateTempSubdirectory("SnippetPredictor.Benchmark.")
    let path = Path.Combine(directory.FullName, ".snippet-predictor.json")
    let client = PredictionClient("benchmark", PredictionClientKind.Terminal)

    let input =
        match scenario with
        | Scenario.NoMatch -> "missing"
        | Scenario.EmptyQuery -> ":snp"
        | Scenario.Snp -> ":snp git"
        | Scenario.Group -> ":git git"
        | Scenario.Tooltip -> ":tip git"
        | _ -> "git"

    let context = PredictionContext.Create(input)

    let entries, cache, predictor =
        try
            let samples =
                match scenario with
                | Scenario.Long -> [| String.replicate 1000 "x" + " git" |]
                | Scenario.Repeated -> [| String.replicate 100 "digit " + "git" |]
                | Scenario.InternalTail -> [| "digit" + String.replicate 10000 "x" |]
                | Scenario.Unicode -> [| "égit"; "\U00010400git"; "a\u0301git"; "\U0001F600git"; "git" |]
                | _ -> [| "git"; "git status"; "Write-Host git"; "digit"; "unrelated" |]

            let entries =
                Array.init count (fun index ->
                    let text =
                        if scenario = Scenario.Sparse then
                            if index = count - 1 then "git" else "unrelated"
                        else
                            samples[index % samples.Length]

                    {|
                        Snippet = text
                        Tooltip =
                            if scenario = Scenario.InternalTail then
                                "internal match"
                            else
                                samples[(index + 1) % samples.Length]
                        Group = if index % 2 = 0 then "git" else "other"
                    |})

            File.WriteAllText(path, JsonSerializer.Serialize({| Snippets = entries |}))
            let cache = new Suggestion.Cache()

            try
                let predictor =
                    new SnippetPredictor(Guid.NewGuid().ToString(), fun () -> directory.FullName, path)

                entries, cache, predictor
            with _ ->
                (cache :> IDisposable).Dispose()
                reraise ()
        with _ ->
            directory.Delete(true)
            reraise ()

    let dispose () =
        try
            (predictor :> IDisposable).Dispose()
        finally
            try
                (cache :> IDisposable).Dispose()
            finally
                directory.Delete(true)

    let ready () =
        cache.getCompletionTexts(":snp").Length = count
        && predictor.GetCompletionTexts(":snp").Length = count

    let waitUntilReady () =
        let stopwatch = Stopwatch.StartNew()

        while not (ready ()) && stopwatch.ElapsedMilliseconds < 10000L do
            Thread.Sleep 20

        if not (ready ()) then
            invalidOp "Benchmark configuration did not finish loading."

    let validateCandidates () =
        let query =
            if scenario = Scenario.EmptyQuery then ""
            elif scenario = Scenario.NoMatch then "missing"
            else "git"

        let expected =
            entries
            |> Array.filter (fun entry ->
                let text =
                    if scenario = Scenario.Tooltip then
                        entry.Tooltip
                    else
                        entry.Snippet

                (scenario <> Scenario.Group || entry.Group = "git")
                && text.Contains(query, StringComparison.OrdinalIgnoreCase))
            |> Array.length

        let cached = cache.getPredictiveSuggestions input

        let predicted =
            (predictor :> ICommandPredictor).GetSuggestion(client, context, CancellationToken.None)

        let predictedCount =
            match predicted.SuggestionEntries with
            | null -> 0
            | entries -> entries.Count

        if cached.Count <> expected || predictedCount <> expected then
            invalidOp "Benchmark fixture returned an unexpected number of candidates."

    do
        try
            cache.load (fun () -> directory.FullName, path)
            waitUntilReady ()
            validateCandidates ()
        with _ ->
            dispose ()
            reraise ()

    member _.CacheSuggestions() = cache.getPredictiveSuggestions input

    member _.PredictorSuggestions() =
        (predictor :> ICommandPredictor).GetSuggestion(client, context, CancellationToken.None)

    interface IDisposable with
        member _.Dispose() = dispose ()

[<MemoryDiagnoser>]
type PredictionBenchmarks() =
    let mutable fixture: Fixture option = None

    let getFixture () =
        match fixture with
        | Some value -> value
        | None -> invalidOp "Benchmark fixture has not been initialized."

    [<Params(1000, 10000)>]
    member val Count = 1000 with get, set

    [<ParamsAllValues>]
    member val Scenario = Scenario.Mixed with get, set

    [<GlobalSetup>]
    member this.Setup() =
        fixture <- Some(new Fixture(this.Count, this.Scenario))

    [<GlobalCleanup>]
    member _.Cleanup() =
        fixture |> Option.iter (fun value -> (value :> IDisposable).Dispose())
        fixture <- None

    [<Benchmark>]
    member _.CacheSuggestions() = (getFixture ()).CacheSuggestions()

    [<Benchmark>]
    member _.GetSuggestion() = (getFixture ()).PredictorSuggestions()

module Program =
    [<EntryPoint>]
    let main args =
        let summaries =
            BenchmarkSwitcher.FromAssembly(typeof<PredictionBenchmarks>.Assembly).Run(args)
            |> Seq.toArray

        if
            summaries.Length = 0
            || summaries
               |> Array.exists (fun summary ->
                   summary.HasCriticalValidationErrors
                   || summary.Reports |> Seq.exists (fun report -> not report.Success))
        then
            1
        else
            0
