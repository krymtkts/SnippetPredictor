namespace SnippetPredictor

module Store =
    val getSnippetPath: (unit -> string)

    val loadConfig: getSnippetPath: (unit -> string) -> Config.ConfigState

    val makeErrorRecord: e: string -> System.Management.Automation.ErrorRecord

    val makeSnippetEntry: snippet: string -> tooltip: string -> group: string | null -> SnippetEntry

    val loadSnippets: getSnippetPath: (unit -> string) -> Result<SnippetEntry array, string>

    val addSnippets: getSnippetPath: (unit -> string) -> snippets: SnippetEntry seq -> Result<unit, string>

    val removeSnippets: getSnippetPath: (unit -> string) -> snippets: string seq -> Result<unit, string>
