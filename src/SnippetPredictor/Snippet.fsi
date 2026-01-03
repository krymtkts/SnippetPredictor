namespace SnippetPredictor

module Nullable =
    val dispose: d: #System.IDisposable | null -> unit

#if DEBUG
type GroupJsonConverter =
    inherit System.Text.Json.Serialization.JsonConverter<string>

    new: unit -> GroupJsonConverter

    override Read:
        reader: byref<System.Text.Json.Utf8JsonReader> *
        _typeToConvert: System.Type *
        options: System.Text.Json.JsonSerializerOptions ->
            string

    override Write:
        writer: System.Text.Json.Utf8JsonWriter * value: string * options: System.Text.Json.JsonSerializerOptions ->
            unit
#endif

module Group =
    [<Literal>]
    val pattern: string = "^[A-Za-z0-9]+$"

type SnippetEntry =
    { Snippet: string
      Tooltip: string
      Group: string | null }

type SnippetConfig =
    { SearchCaseSensitive: bool
      Snippets: SnippetEntry array | null }

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
