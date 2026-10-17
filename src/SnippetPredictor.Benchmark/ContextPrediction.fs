namespace SnippetPredictorBenchmark

open System
open System.Diagnostics
open System.IO
open System.Management.Automation.Subsystem.Prediction
open System.Text.Json
open System.Threading
open BenchmarkDotNet.Attributes
open SnippetPredictor

type ContextScenario =
    | Semicolon = 0
    | BlockComment = 1
    | Pipeline = 2
    | NoMatch = 3
    | Restricted = 4
    | Disallowed = 5

type ContextFixture(count: int, scenario: ContextScenario) =
    let directory =
        Directory.CreateTempSubdirectory("SnippetPredictor.ContextBenchmark.")

    let path = Path.Combine(directory.FullName, ".snippet-predictor.json")
    let client = PredictionClient("context benchmark", PredictionClientKind.Terminal)

    let leadingInput =
        match scenario with
        | ContextScenario.BlockComment -> "<# benchmark #> "
        | ContextScenario.Pipeline
        | ContextScenario.Disallowed -> "Get-Content file | "
        | _ -> "Write-Host 'start.'; "

    let query =
        if scenario = ContextScenario.NoMatch then
            "missing"
        else
            "git"

    let context = PredictionContext.Create(leadingInput + query)
    let samples = [| "git"; "git status"; "Write-Host git"; "digit"; "unrelated" |]

    let predictor =
        try
            let entries =
                Array.init count (fun index ->
                    {|
                        Snippet = samples[index % samples.Length]
                        Tooltip = "benchmark"
                        PredictionContexts =
                            if scenario = ContextScenario.Disallowed then
                                [| "Command" |]
                            else
                                [| "Command"; "Pipeline" |]
                    |})

            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    {|
                        Snippets = entries
                        PredictOnlyWithIdentifier = scenario = ContextScenario.Restricted
                    |}
                )
            )

            new SnippetPredictor(Guid.NewGuid().ToString(), fun () -> directory.FullName, path)
        with _ ->
            directory.Delete(true)
            reraise ()

    let dispose () =
        try
            (predictor :> IDisposable).Dispose()
        finally
            directory.Delete(true)

    let suggest () =
        (predictor :> ICommandPredictor).GetSuggestion(client, context, CancellationToken.None)

    let validate () =
        let stopwatch = Stopwatch.StartNew()

        while predictor.GetCompletionTexts(":snp").Length <> count
              && stopwatch.ElapsedMilliseconds < 10000L do
            Thread.Sleep 20

        if predictor.GetCompletionTexts(":snp").Length <> count then
            invalidOp "Context benchmark configuration did not finish loading."

        let expected =
            if
                scenario = ContextScenario.NoMatch
                || scenario = ContextScenario.Restricted
                || scenario = ContextScenario.Disallowed
            then
                0
            else
                count - count / samples.Length

        match (suggest ()).SuggestionEntries with
        | null ->
            if expected <> 0 then
                invalidOp "Context benchmark returned no candidates."
        | suggestions ->
            if suggestions.Count <> expected then
                invalidOp "Context benchmark returned an unexpected number of candidates."

            for suggestion in suggestions do
                if not (suggestion.SuggestionText.StartsWith(leadingInput, StringComparison.Ordinal)) then
                    invalidOp "Context benchmark did not preserve leading input."

    do
        try
            validate ()
        with _ ->
            dispose ()
            reraise ()

    member _.GetSuggestion() = suggest ()

    interface IDisposable with
        member _.Dispose() = dispose ()

[<MemoryDiagnoser>]
type ContextPredictionBenchmarks() =
    let mutable fixture: ContextFixture option = None

    [<Params(1000, 10000)>]
    member val Count = 1000 with get, set

    [<ParamsAllValues>]
    member val Scenario = ContextScenario.Semicolon with get, set

    [<GlobalSetup>]
    member this.Setup() =
        fixture <- Some(new ContextFixture(this.Count, this.Scenario))

    [<GlobalCleanup>]
    member _.Cleanup() =
        fixture |> Option.iter (fun value -> (value :> IDisposable).Dispose())
        fixture <- None

    [<Benchmark>]
    member _.GetContextualSuggestion() =
        match fixture with
        | Some value -> value.GetSuggestion()
        | None -> invalidOp "Context benchmark fixture has not been initialized."
