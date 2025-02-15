namespace SnippetPredictor

open System
open System.Collections.Generic
open System.Management.Automation
open System.Management.Automation.Subsystem
open System.Management.Automation.Subsystem.Prediction
open System.Threading

module Snippet =
    open System.IO
    open System.Collections
    open System.Text.Json

    type SnippetEntry = { snippet: string; tooltip: string }
    type Snippets = { snippets: SnippetEntry[] }

    [<Literal>]
    let snippetFilesName = ".snippet-predictor.json"

    [<Literal>]
    let snippetSymbol = ":snp"

    let snippets = Concurrent.ConcurrentQueue<SnippetEntry>()

    let load () =
        let snippetPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), snippetFilesName)

        let cancellationToken = new CancellationToken()

        if File.Exists(snippetPath) then
            task {
                let! json = snippetPath |> File.ReadAllTextAsync

                json
                |> JsonSerializer.Deserialize<Snippets>
                |> _.snippets
                |> Array.iter snippets.Enqueue
            }
            |> _.WaitAsync(cancellationToken)
            |> ignore

    let get (filter: string) : SnippetEntry seq =
        snippets |> Seq.filter _.snippet.Contains(filter)

    let getPredictiveSuggestions (filter: string) : List<PredictiveSuggestion> =
        if String.IsNullOrWhiteSpace(filter) then
            // NOTE: cannot pass null.
            Seq.empty
        else
            // NOTE: Remove the snippet symbol from the input.
            // NOTE: Snippet symbol is used to exclude other predictors from suggestions.
            filter.Replace(snippetSymbol, "")
            |> get
            |> Seq.map (fun s -> s.snippet, s.tooltip)
            |> Seq.map PredictiveSuggestion
        |> Linq.Enumerable.ToList

type SamplePredictor(guid: string) =
    let id = Guid.Parse(guid)

    [<Literal>]
    let name = "Snippet"

    [<Literal>]
    let description = "A predictor that suggests a snippet based on the input."

    do Snippet.load ()

    interface ICommandPredictor with
        member __.Id = id
        member __.Name = name
        member __.Description: string = description
        member __.FunctionsToDefine: Dictionary<string, string> = Dictionary<string, string>()

        member __.GetSuggestion
            (client: PredictionClient, context: PredictionContext, cancellationToken: CancellationToken)
            : SuggestionPackage =
            context.InputAst.Extent.Text
            |> Snippet.getPredictiveSuggestions
            |> SuggestionPackage

        member __.CanAcceptFeedback(client: PredictionClient, feedback: PredictorFeedbackKind) : bool = false

        member __.OnSuggestionDisplayed(client: PredictionClient, session: uint32, countOrIndex: int) : unit = ()

        member __.OnSuggestionAccepted(client: PredictionClient, session: uint32, acceptedSuggestion: string) : unit =
            ()

        member __.OnCommandLineAccepted(client: PredictionClient, history: IReadOnlyList<string>) : unit = ()

        member __.OnCommandLineExecuted(client: PredictionClient, commandLine: string, success: bool) : unit = ()

type Init() =
    [<Literal>]
    let identifier = "f6dbcf05-2f90-4c47-b40e-6a4cec337cc1"

    interface IModuleAssemblyInitializer with
        member __.OnImport() =
            let predictor = SamplePredictor(identifier)
            SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, predictor)

    interface IModuleAssemblyCleanup with
        member __.OnRemove(psModuleInfo: PSModuleInfo) =
            SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, Guid(identifier))
