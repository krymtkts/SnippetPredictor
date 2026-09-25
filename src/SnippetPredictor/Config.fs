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

    let makeErrorEntry (errorMessage: string) (errorDetail: string) : ErrorEntry =
        {
            Snippet = $"'{errorMessage}'" // NOTE: Wrap in quotes to avoid errors if the error message is executed.
            Tooltip = errorDetail
            Group = null
        }


    [<RequireQualifiedAccess>]
    [<NoEquality>]
    [<NoComparison>]
    type ConfigState =
        | Empty
        | Valid of config: SnippetConfig
        | Invalid of errorEntry: ErrorEntry

    let jsonOptions =
        JsonSerializerOptions(
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        )

    let private jsonReadOptions =
        let options = JsonSerializerOptions(jsonOptions)
        // NOTE: Register this read-only converter only for deserialization.
        options.Converters.Add(SnippetEntryJsonConverter())
        options

    let parseSnippets (json: string) =
        try
            json.Trim()
            |> function
                | json when String.length json = 0 -> ConfigState.Empty
                | json ->
                    JsonSerializer.Deserialize<SnippetConfig>(json, jsonReadOptions)
                    |> function
                        | null ->
                            makeErrorEntry $"{snippetFilesName} is null or invalid format." ""
                            |> ConfigState.Invalid
                        | config -> ConfigState.Valid config
        with e ->
            let errorDetail =
                match e with
                | :? SnippetConfigValidationException as validationError ->
                    $"{validationError.Detail} Path: {validationError.Path}"
                | _ -> e.Message

            makeErrorEntry $"An error occurred while parsing {snippetFilesName}" errorDetail
            |> ConfigState.Invalid

    let private readToConfigState (read: Result<string option, string>) =
        match read with
        | Ok None -> ConfigState.Empty
        | Ok(Some json) -> parseSnippets json
        | Error errorDetail ->
            makeErrorEntry $"An error occurred while reading {snippetFilesName}" errorDetail
            |> ConfigState.Invalid

    let private isExpectedReadError (error: exn) =
        match error with
        | :? IOException
        | :? UnauthorizedAccessException
        | :? Security.SecurityException
        | :? ArgumentException
        | :? NotSupportedException -> true
        | _ -> false

    // NOTE: Avoid checking File.Exists first; it returns false on determination errors,
    // NOTE: including insufficient permissions, which would hide read failures.
    // NOTE: Open files with shared read/write access to prevent file lock errors from other processes.
    let private readSnippetFile (path: string) =
        task {
            try
                use fs =
                    new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync = true)

                use sr = new StreamReader(fs)
                let! json = sr.ReadToEndAsync()
                return Ok(Some json)
            with
            | :? FileNotFoundException -> return Ok None
            | error when isExpectedReadError error -> return Error error.Message
        }

    let private readSnippetFileSync (path: string) =
        try
            use fs =
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096)

            use sr = new StreamReader(fs)
            sr.ReadToEnd() |> Some |> Ok
        with
        | :? FileNotFoundException -> Ok None
        | error when isExpectedReadError error -> Error error.Message

    let internal parseSnippetFileSync (path: string) =
        path |> readSnippetFileSync |> readToConfigState

    let parseSnippetFile (path: string) =
        task {
            let! read = readSnippetFile path
            return read |> readToConfigState
        }

    let getSnippetPathWith (getEnvironmentVariable: string -> string | null) (getUserProfilePath: unit -> string) =
        let snippetDirectory =
            // NOTE: Split branches to narrow the type (string | null)
            match getEnvironmentVariable environmentVariable with
            | null -> getUserProfilePath ()
            | path when String.length path = 0 -> getUserProfilePath ()
            | path -> path

        snippetDirectory, Path.Combine(snippetDirectory, snippetFilesName)

    let getUserProfilePath () =
        Environment.GetFolderPath Environment.SpecialFolder.UserProfile

    let getSnippetPath () =
        getSnippetPathWith Environment.GetEnvironmentVariable getUserProfilePath

    let storeConfig (getSnippetPath: unit -> string) (config: SnippetConfig) =
        let json = JsonSerializer.Serialize(config, jsonOptions)
        let snippetPath = getSnippetPath ()

        try
            File.WriteAllText(snippetPath, json)
            Ok()
        with e ->
            e.Message |> Error
