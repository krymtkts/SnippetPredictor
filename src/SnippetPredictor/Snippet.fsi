namespace SnippetPredictor

module Snippet =
    [<Literal>]
    val name: string = "Snippet"

    [<RequireQualifiedAccess; NoEquality; NoComparison>]
    type ConfigState =
        | Empty
        | Valid of SnippetConfig
        | Invalid of SnippetEntry

    val parseSnippets: json: string -> ConfigState

#if DEBUG
    module Disposal =
        type Flag =
            new: unit -> Flag
            member IsDisposed: bool with get
            member TryMarkDisposed: unit -> bool
            member IfNotDisposed: f: (unit -> unit) -> unit
            member IfDisposed: f: (unit -> unit) -> unit
#endif

    type Cache =
        interface System.IDisposable

        new: unit -> Cache

        abstract CreateWatcher: directory: string * filter: string -> System.IO.FileSystemWatcher

        abstract OnRefresh: path: string -> unit

        member getPredictiveSuggestions:
            input: string ->
                System.Collections.Generic.List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>

        member load: getSnippetPath: (unit -> string * string) -> unit

    val getSnippetPathWith:
        getEnvironmentVariable: (string -> string | null) -> getUserProfilePath: (unit -> string) -> string * string

    val getSnippetPath: unit -> string * string

    val loadConfig: getSnippetPath: (unit -> string * string) -> ConfigState

    val makeErrorRecord: e: string -> System.Management.Automation.ErrorRecord

    val makeSnippetEntry: snippet: string -> tooltip: string -> group: string | null -> SnippetEntry

    val loadSnippets: getSnippetPath: (unit -> string * string) -> Result<SnippetEntry array, string>

    val addSnippets: getSnippetPath: (unit -> string * string) -> snippets: SnippetEntry seq -> Result<unit, string>

    val removeSnippets: getSnippetPath: (unit -> string * string) -> snippets: string seq -> Result<unit, string>
