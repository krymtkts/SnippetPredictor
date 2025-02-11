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
    open System.Text

    let snippets = Concurrent.ConcurrentQueue<string>()

    let load () =
        let snippetPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".snippets")

        let cancellationToken = new CancellationToken()

        if File.Exists(snippetPath) then
            task {
                let! lines = File.ReadAllBytesAsync(snippetPath)

                Encoding.UTF8.GetString(lines).Split([| '\n' |]) |> Array.iter snippets.Enqueue
            }
            |> _.WaitAsync(cancellationToken)
            |> ignore

    let get (filter: string) : string seq =
        snippets |> Seq.filter (fun s -> s.Contains(filter))

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
            let input = context.InputAst.Extent.Text

            if String.IsNullOrWhiteSpace(input) then
                // NOTE: cannot pass null.
                Seq.empty
            else
                Snippet.get input |> Seq.map PredictiveSuggestion
            |> List<PredictiveSuggestion>
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
