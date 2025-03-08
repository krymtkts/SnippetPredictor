namespace SnippetPredictor

type SnippetEntry = { Snippet: string; Tooltip: string }

type SnippetConfig = { Snippets: SnippetEntry array | null }

module Snippet =
    // TODO: encapsulate the snippets.
    val snippets: System.Collections.Concurrent.ConcurrentQueue<SnippetEntry>

    [<RequireQualifiedAccess; NoEquality; NoComparison>]
    type ConfigState =
        | Empty
        | Valid of SnippetConfig
        | Invalid of SnippetEntry

    val parseSnippets: json: string -> ConfigState

    val getSnippetPathWith:
        getEnvironmentVariable: (string -> string | null) -> getUserProfilePath: (unit -> string) -> string * string

    val load: unit -> unit

    val getPredictiveSuggestions:
        input: string ->
            System.Collections.Generic.List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>

    val loadConfig: unit -> ConfigState

    val makeErrorRecord: e: string -> System.Management.Automation.ErrorRecord

    val makeSnippetEntry: snippet: string -> tooltip: string -> SnippetEntry

    val loadSnippets: unit -> Result<SnippetEntry array, string>

    val addSnippets: snippets: SnippetEntry seq -> Result<unit, string>

    val removeSnippets: snippets: string seq -> Result<unit, string>
