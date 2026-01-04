namespace SnippetPredictor

module Store =
    [<Literal>]
    val name: string = "Snippet"

    val getSnippetPath: (unit -> string * string)

    val loadConfig: getSnippetPath: (unit -> string * string) -> Config.ConfigState

    val makeErrorRecord: e: string -> System.Management.Automation.ErrorRecord

    val makeSnippetEntry: snippet: string -> tooltip: string -> group: string | null -> SnippetEntry

    val loadSnippets: getSnippetPath: (unit -> string * string) -> Result<SnippetEntry array, string>

    val addSnippets: getSnippetPath: (unit -> string * string) -> snippets: SnippetEntry seq -> Result<unit, string>

    val removeSnippets: getSnippetPath: (unit -> string * string) -> snippets: string seq -> Result<unit, string>
