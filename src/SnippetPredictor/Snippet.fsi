namespace SnippetPredictor

type SnippetEntry = { Snippet: string; Tooltip: string }

type SnippetConfig = { Snippets: SnippetEntry array | null }

module Snippet =
    [<RequireQualifiedAccess; NoEquality; NoComparison>]
    type ConfigState =
        | Empty
        | Valid of SnippetConfig
        | Invalid of SnippetEntry

    val parseSnippets: json: string -> ConfigState

    type Cache =
        new: unit -> Cache

        member getPredictiveSuggestions:
            input: string ->
                System.Collections.Generic.List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>

        member load: getSnippetPath: (unit -> string * string) -> unit

    val getSnippetPathWith:
        getEnvironmentVariable: (string -> string | null) -> getUserProfilePath: (unit -> string) -> string * string

    val getSnippetPath: unit -> string * string


    val loadConfig: getSnippetPath: (unit -> string * string) -> ConfigState

    val makeErrorRecord: e: string -> System.Management.Automation.ErrorRecord

    val makeSnippetEntry: snippet: string -> tooltip: string -> SnippetEntry

    val loadSnippets: getSnippetPath: (unit -> string * string) -> Result<SnippetEntry array, string>

    val addSnippets: getSnippetPath: (unit -> string * string) -> snippets: SnippetEntry seq -> Result<unit, string>

    val removeSnippets: getSnippetPath: (unit -> string * string) -> snippets: string seq -> Result<unit, string>
