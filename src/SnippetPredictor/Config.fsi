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

#if DEBUG
    val parseSnippets: json: string -> ConfigState
#endif

    val parseSnippetFile: path: string -> Task<ConfigState>

#if DEBUG
    val getSnippetPathWith:
        getEnvironmentVariable: (string -> string | null) -> getUserProfilePath: (unit -> string) -> string * string
#endif

    val getSnippetPath: unit -> string * string

    val storeConfig: getSnippetPath: (unit -> string) -> SnippetConfig -> Result<unit, string>
