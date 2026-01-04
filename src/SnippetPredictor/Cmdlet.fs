namespace SnippetPredictor

open System.Collections.Generic
open System.Management.Automation

[<Cmdlet(VerbsCommon.Get, Snippet.name)>]
[<OutputType(typeof<SnippetEntry>)>]
type GetSnippetCommand() =
    inherit Cmdlet()

    abstract member GetSnippetPath: unit -> string * string
    default __.GetSnippetPath() = Snippet.getSnippetPath ()

    override __.EndProcessing() =
        Snippet.loadSnippets __.GetSnippetPath
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
    [<ValidatePattern(Group.pattern)>]
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

    abstract member GetSnippetPath: unit -> string * string
    default __.GetSnippetPath() = Snippet.getSnippetPath ()

    override __.ProcessRecord() = __.Snippet |> snippets.Add

    override __.EndProcessing() =
        snippets
        |> Snippet.removeSnippets __.GetSnippetPath
        |> function
            | Ok() -> ()
            | Error e -> e |> Snippet.makeErrorRecord |> __.WriteError
