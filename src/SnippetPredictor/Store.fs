namespace SnippetPredictor

module Store =
    open System
    open System.IO
    open System.Management.Automation

    open Config

    let getSnippetPath = Config.getSnippetPath >> snd

    let toError (e: SnippetEntry) =
        if String.IsNullOrEmpty e.Tooltip then
            e.Snippet
        else
            $"{e.Snippet}: {e.Tooltip}"
        |> Error

    let loadConfig (getSnippetPath: unit -> string) =
        let snippetPath = getSnippetPath ()
        snippetPath |> parseSnippetFileSync

    let makeErrorRecord (e: string) =
        new ErrorRecord(new Exception(e), "", ErrorCategory.InvalidData, null)

    let makeSnippetEntry
        (predictionContexts: PredictionContexts array)
        (snippet: string)
        (tooltip: string)
        (group: string | null)
        =
        let permissions =
            match predictionContexts with
            | contexts when contexts.Length = 0 ->
                invalidArg (nameof predictionContexts) "Specify at least one prediction context."
            | contexts ->
                let permissions =
                    contexts
                    |> Array.fold
                        (fun permissions context ->
                            if permissions |> PredictionContexts.overlaps context then
                                invalidArg (nameof predictionContexts) "Prediction contexts must not overlap."

                            permissions ||| context)
                        PredictionContexts.IdentifierOnly

                if not (permissions |> PredictionContexts.isValid) then
                    invalidArg (nameof predictionContexts) "Specify Command, Pipeline, or IdentifierOnly."

                if
                    contexts.Length > 1
                    && contexts |> Array.contains PredictionContexts.IdentifierOnly
                then
                    invalidArg (nameof predictionContexts) "IdentifierOnly must be specified alone."

                permissions

        {
            Snippet = snippet
            Tooltip = tooltip
            Group = group
            PredictionContexts = permissions
        }

    let loadSnippets getSnippetPath =
        loadConfig getSnippetPath
        |> function
            | ConfigState.Empty -> Array.empty |> Ok
            | ConfigState.Valid snippets ->
                snippets
                |> _.Snippets
                |> function
                    | null -> Array.empty |> Ok
                    | snps -> snps |> Ok
            | ConfigState.Invalid e -> e |> toError

    let addSnippets (getSnippetPath: (unit -> string)) (snippets: SnippetEntry seq) =
        loadConfig getSnippetPath
        |> function
            | ConfigState.Empty ->
                {
                    SearchCaseSensitive = false
                    PredictOnlyWithIdentifier = false
                    Snippets = Array.ofSeq snippets
                }
                |> storeConfig getSnippetPath
            | ConfigState.Valid config ->
                let newSnippets =
                    config.Snippets
                    |> function
                        | null -> Array.ofSeq snippets
                        | snps -> Array.append snps <| Array.ofSeq snippets

                { config with Snippets = newSnippets } |> storeConfig getSnippetPath
            | ConfigState.Invalid e -> e |> toError

    let removeSnippets (getSnippetPath: (unit -> string)) (snippets: string seq) =
        loadConfig getSnippetPath
        |> function
            | ConfigState.Empty -> Ok()
            | ConfigState.Valid config ->
                let newSnippets =
                    config.Snippets
                    |> function
                        | null -> Array.empty
                        | snps ->
                            let removals = set snippets
                            snps |> Array.filter (_.Snippet >> removals.Contains >> not)

                { config with Snippets = newSnippets } |> storeConfig getSnippetPath
            | ConfigState.Invalid e -> e |> toError
