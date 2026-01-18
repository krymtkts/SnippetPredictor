namespace SnippetPredictor

module Config =
    open System.Threading.Tasks

    [<Literal>]
    val snippetFilesName: string = ".snippet-predictor.json"

    [<Literal>]
    val environmentVariable: string = "SNIPPET_PREDICTOR_CONFIG"

    [<RequireQualifiedAccess; NoEquality; NoComparison>]
    type ConfigState =
        | Empty
        | Valid of config: SnippetConfig
        | Invalid of errorEntry: ErrorEntry

    // TODO: currently visible for testing.
    val parseSnippets: json: string -> ConfigState

    val parseSnippetFile: path: string -> Task<ConfigState>

#if DEBUG
    val getSnippetPathWith:
        getEnvironmentVariable: (string -> string | null) -> getUserProfilePath: (unit -> string) -> string * string
#endif

    val getSnippetPath: unit -> string * string

    val storeConfig: (unit -> 'a * string) -> SnippetConfig -> Result<unit, string>
