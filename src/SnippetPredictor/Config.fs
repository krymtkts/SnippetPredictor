namespace SnippetPredictor

module Config =
    open System
    open System.IO
    open System.Text.Json
    open System.Text.Encodings.Web

    [<Literal>]
    let snippetFilesName = ".snippet-predictor.json"

    [<Literal>]
    let environmentVariable = "SNIPPET_PREDICTOR_CONFIG"

    let readSnippetFile (path: string) =
        task {
            // NOTE: Open the file with shared read/write access to prevent the file lock error by other processes.
            use fs =
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync = true)

            use sr = new StreamReader(fs)
            return! sr.ReadToEndAsync()
        }

    let makeEntry (snippet: string) (tooltip: string) =
        { Snippet = $"'{snippet}'"
          Tooltip = tooltip
          Group = null }

    [<RequireQualifiedAccess>]
    [<NoEquality>]
    [<NoComparison>]
    type ConfigState =
        | Empty
        | Valid of SnippetConfig
        | Invalid of SnippetEntry

    let jsonOptions =
        JsonSerializerOptions(
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        )

    let parseSnippets (json: string) =
        try
            json.Trim()
            |> function
                | json when String.length json = 0 -> ConfigState.Empty
                | json ->
                    JsonSerializer.Deserialize<SnippetConfig>(json, jsonOptions)
                    |> function
                        | null ->
                            makeEntry $"{snippetFilesName} is null or invalid format." ""
                            |> ConfigState.Invalid
                        | snippets -> ConfigState.Valid snippets
        with e ->
            makeEntry $"An error occurred while parsing {snippetFilesName}" e.Message
            |> ConfigState.Invalid

    let parseSnippetFile (path: string) =
        task {
            let! json = readSnippetFile path
            return parseSnippets json
        }

    let getSnippetPathWith (getEnvironmentVariable: string -> string | null) (getUserProfilePath: unit -> string) =
        let snippetDirectory =
            // NOTE: Split branches to narrow the type (string | null)
            match getEnvironmentVariable environmentVariable with
            | null -> getUserProfilePath ()
            | path when String.length path = 0 -> getUserProfilePath ()
            | path -> path

        snippetDirectory, Path.Combine(snippetDirectory, snippetFilesName)

    let getSnippetPath () =
        getSnippetPathWith Environment.GetEnvironmentVariable
        <| fun () -> Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)

    let storeConfig getSnippetPath (config: SnippetConfig) =
        let json = JsonSerializer.Serialize(config, jsonOptions)
        let snippetPath = getSnippetPath () |> snd

        try
            File.WriteAllText(snippetPath, json)
            Ok()
        with e ->
            e.Message |> Error
