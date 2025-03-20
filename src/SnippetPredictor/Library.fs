namespace SnippetPredictor

open System
open System.Collections.Generic
open System.Management.Automation
open System.Management.Automation.Subsystem
open System.Management.Automation.Subsystem.Prediction
open System.Threading

type SnippetPredictor(guid: string) =
    let id = Guid.Parse(guid)

    [<Literal>]
    let name = Snippet.name

    [<Literal>]
    let description = "A predictor that suggests a snippet based on the input."

    let cache = Snippet.Cache()

    do cache.load Snippet.getSnippetPath

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
            let predictor = SnippetPredictor(identifier)
            SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, predictor)

    interface IModuleAssemblyCleanup with
        member __.OnRemove(psModuleInfo: PSModuleInfo) =
            SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, Guid(identifier))

[<Cmdlet(VerbsCommon.Get, Snippet.name)>]
[<OutputType(typeof<SnippetEntry[]>)>]
type GetSnippetCommand() =
    inherit Cmdlet()

    override __.EndProcessing() =
        Snippet.loadSnippets Snippet.getSnippetPath
        |> function
            | Ok snippets -> snippets |> Seq.iter __.WriteObject
            | Error e -> e |> Snippet.makeErrorRecord |> __.WriteError

[<Cmdlet(VerbsCommon.Add, Snippet.name)>]
type AddSnippetCommand() =
    inherit Cmdlet()

    let snippets = List<SnippetEntry>()

    [<Parameter(Position = 0,
                Mandatory = true,
                ValueFromPipeline = true,
                ValueFromPipelineByPropertyName = true,
                HelpMessage = "The text of the snippet")>]
    member val Snippet = "" with get, set

    [<Parameter(Position = 1,
                Mandatory = false,
                ValueFromPipelineByPropertyName = true,
                HelpMessage = "The tooltip of the snippet")>]
    member val Tooltip = "" with get, set

    [<Parameter(Position = 2,
                Mandatory = false,
                ValueFromPipelineByPropertyName = true,
                HelpMessage = "The group of the snippet")>]
    [<ValidatePattern("^[A-Za-z0-9]+$")>]
    member val Group: string | null = null with get, set

    abstract member GetSnippetPath: unit -> string * string
    default __.GetSnippetPath() = Snippet.getSnippetPath ()

    override __.ProcessRecord() =
        Snippet.makeSnippetEntry __.Snippet __.Tooltip __.Group |> snippets.Add

    override __.EndProcessing() =
        snippets
        |> Snippet.addSnippets __.GetSnippetPath
        |> function
            | Ok() -> ()
            | Error e -> e |> Snippet.makeErrorRecord |> __.WriteError

[<Cmdlet(VerbsCommon.Remove, Snippet.name)>]
type RemoveSnippetCommand() =
    inherit Cmdlet()

    let snippets = List<string>()

    [<Parameter(Position = 0,
                Mandatory = true,
                ValueFromPipeline = true,
                ValueFromPipelineByPropertyName = true,
                HelpMessage = "The text of the snippet to remove")>]
    member val Snippet = "" with get, set

    override __.ProcessRecord() = __.Snippet |> snippets.Add

    override __.EndProcessing() =
        snippets
        |> Snippet.removeSnippets Snippet.getSnippetPath
        |> function
            | Ok() -> ()
            | Error e -> e |> Snippet.makeErrorRecord |> __.WriteError
