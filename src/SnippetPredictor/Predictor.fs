namespace SnippetPredictor

open System
open System.Collections.Generic
open System.Management.Automation
open System.Management.Automation.Subsystem
open System.Management.Automation.Subsystem.Prediction
open System.Threading

type SnippetPredictor(guid: string, getSnippetPath: unit -> string * string) =
    let id = Guid.Parse(guid)

    [<Literal>]
    let name = Suggestion.name

    [<Literal>]
    let description = "A predictor that suggests a snippet based on the input."

    let cache = new Suggestion.Cache()

    do cache.load getSnippetPath

    interface ICommandPredictor with
        member __.Id = id
        member __.Name = name
        member __.Description: string = description
        member __.FunctionsToDefine: Dictionary<string, string> = Dictionary<string, string>()

        member __.GetSuggestion
            (client: PredictionClient, context: PredictionContext, cancellationToken: CancellationToken)
            : SuggestionPackage =
            context.InputAst.Extent.Text
            |> cache.getPredictiveSuggestions
            |> function
                // NOTE: suggestionEntries requires non-empty by Requires.NotNullOrEmpty.
                // https://github.com/PowerShell/PowerShell/blob/eef334de1b0f648512859bd032356f9c8df7cb91/src/System.Management.Automation/engine/Subsystem/PredictionSubsystem/ICommandPredictor.cs#L278
                | suggestions when suggestions.Count = 0 -> SuggestionPackage()
                | suggestions -> SuggestionPackage(suggestions)

        member __.CanAcceptFeedback(client: PredictionClient, feedback: PredictorFeedbackKind) : bool = false

        member __.OnSuggestionDisplayed(client: PredictionClient, session: uint32, countOrIndex: int) : unit = ()

        member __.OnSuggestionAccepted(client: PredictionClient, session: uint32, acceptedSuggestion: string) : unit =
            ()

        member __.OnCommandLineAccepted(client: PredictionClient, history: IReadOnlyList<string>) : unit = ()

        member __.OnCommandLineExecuted(client: PredictionClient, commandLine: string, success: bool) : unit = ()

    interface IDisposable with
        member __.Dispose() = (cache :> IDisposable).Dispose()

type Init() =
    [<Literal>]
    let identifier = "f6dbcf05-2f90-4c47-b40e-6a4cec337cc1"

    let mutable predictor: SnippetPredictor | null = null

    interface IModuleAssemblyInitializer with
        member __.OnImport() =
            let p = new SnippetPredictor(identifier, Suggestion.getSnippetPath)
            SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, p)
            predictor <- p

    interface IModuleAssemblyCleanup with
        member __.OnRemove(psModuleInfo: PSModuleInfo) =
            SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, Guid(identifier))
            predictor |> Nullable.dispose
