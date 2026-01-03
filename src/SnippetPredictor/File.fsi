namespace SnippetPredictor

module File =
    open System.Threading.Tasks

    [<Literal>]
    val snippetFilesName: string = ".snippet-predictor.json"

    [<Literal>]
    val environmentVariable: string = "SNIPPET_PREDICTOR_CONFIG"

    [<RequireQualifiedAccess; NoEquality; NoComparison>]
    type ConfigState =
        | Empty
        | Valid of SnippetConfig
        | Invalid of SnippetEntry

    // TODO: currently visible for testing.
    val parseSnippets: json: string -> ConfigState

    val parseSnippetFile: path: string -> Task<ConfigState>

    val storeConfig: (unit -> 'a * string) -> SnippetConfig -> Result<unit, string>
