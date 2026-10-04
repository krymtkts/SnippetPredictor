namespace SnippetPredictor

module Suggestion =
    open System
    open System.Collections
    open System.IO
    open System.Globalization
    open System.Management.Automation.Subsystem.Prediction
    open System.Text.RegularExpressions
    open System.Threading

    open Config

    let getSnippetPath = Config.getSnippetPath

    module CaseSensitivity =
        [<Literal>]
        let sensitive = 1

        [<Literal>]
        let insensitive = 0

    module SearchCaseSensitivity =
        let ofBool =
            function
            | true -> CaseSensitivity.sensitive
            | false -> CaseSensitivity.insensitive

        let stringComparison =
            function
            | CaseSensitivity.sensitive -> StringComparison.Ordinal
            | _ -> StringComparison.OrdinalIgnoreCase

    [<Literal>]
    let Snp = "snp"

    [<Literal>]
    let Tip = "tip"

    let private snpCompletionIdentifiers = [| $":{Snp}" |]

    [<Literal>]
    let private ExactMatchRank = 0

    [<Literal>]
    let private PrefixMatchRank = 1

    [<Literal>]
    let private WordStartMatchRank = 2

    [<Literal>]
    let private SubstringMatchRank = 3

    module Disposal =
        [<Literal>]
        let disposed = 1

        [<Literal>]
        let notDisposed = 0

        type Flag() =

            let mutable status = notDisposed

            member __.IsDisposed = Volatile.Read(&status) = disposed

            member __.TryMarkDisposed() =
                Interlocked.Exchange(&status, disposed) = notDisposed

            member __.IfNotDisposed(f: unit -> unit) = if __.IsDisposed then () else f ()
            member __.IfDisposed(f: unit -> unit) = if __.IsDisposed then f () else ()

    type private Snapshot =
        {
            Snippets: SnippetEntry array
            GroupIds: string array
            Groups: Set<string>
            CompletionIdentifiers: string array
            SearchComparison: StringComparison
            HasValidConfiguration: bool
        }

    type Cache() as __ =

        let mutable snapshot =
            {
                Snippets = Array.empty
                GroupIds = Array.empty
                Groups = Set.empty
                CompletionIdentifiers = snpCompletionIdentifiers
                SearchComparison = StringComparison.OrdinalIgnoreCase
                HasValidConfiguration = false
            }

        let semaphore = new SemaphoreSlim(1, 1)
        let refreshCts = new CancellationTokenSource()
        let mutable watcher: FileSystemWatcher | null = null
        let disposed = Disposal.Flag()

        let disposeWatcher (w: FileSystemWatcher | null) =
            match w with
            | null -> ()
            | w ->
                try
                    w.EnableRaisingEvents <- false
                    w.Dispose()
                with :? ObjectDisposedException ->
                    ()

        let exchangeAndDisposeWatcher (newWatcher: FileSystemWatcher | null) =
            let oldWatcher = Interlocked.Exchange(&watcher, newWatcher)
            disposeWatcher oldWatcher

        let tryRemoveCurrentWatcher (expected: FileSystemWatcher) =
            let removed = Interlocked.CompareExchange(&watcher, null, expected)

            if Object.ReferenceEquals(removed, expected) then
                disposeWatcher expected
                true
            else
                false

        let createFallbackSnapshot snippets searchComparison : Snapshot =
            {
                Snippets = snippets
                GroupIds = Array.empty
                Groups = Set.empty
                CompletionIdentifiers = snpCompletionIdentifiers
                SearchComparison = searchComparison
                HasValidConfiguration = false
            }

        let createSnapshot (previous: Snapshot) result =
            match result with
            | ConfigState.Empty -> createFallbackSnapshot Array.empty previous.SearchComparison
            | ConfigState.Invalid errorEntry -> createFallbackSnapshot [| errorEntry |] previous.SearchComparison
            | ConfigState.Valid {
                                    SearchCaseSensitive = searchCaseSensitive
                                    Snippets = entries
                                } ->
                let snippets =
                    match entries with
                    | null -> Array.empty
                    | snippets -> snippets

                let groups = new Concurrent.ConcurrentDictionary<string, unit>()

                snippets
                |> Array.iter (fun snippet ->
                    match snippet.Group with
                    | null
                    | Snp
                    | Tip -> ()
                    | groupId when groups.ContainsKey groupId -> ()
                    | groupId -> groups.TryAdd(groupId, ()) |> ignore)

                let groupIds =
                    groups.Keys
                    |> Seq.toArray
                    |> Array.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))

                let groupLookup = groupIds |> Set.ofArray

                let completionIdentifiers =
                    groupIds
                    |> Array.map (fun groupId -> $":{groupId}")
                    |> Array.append snpCompletionIdentifiers

                {
                    Snippets = snippets
                    GroupIds = groupIds
                    Groups = groupLookup
                    CompletionIdentifiers = completionIdentifiers
                    SearchComparison =
                        searchCaseSensitive
                        |> SearchCaseSensitivity.ofBool
                        |> SearchCaseSensitivity.stringComparison
                    HasValidConfiguration = true
                }

        let startRefreshTask (path: string) =
            let cancellationToken = refreshCts.Token

            task {
                let mutable acquired = false

                try
                    try
                        do! semaphore.WaitAsync(cancellationToken)
                        acquired <- true
#if DEBUG
                        Logger.LogFile [ "Refreshing snippets." ]
#endif

                        let! result = parseSnippetFile path
                        let previous = Volatile.Read(&snapshot)
                        let next = createSnapshot previous result
                        Interlocked.Exchange(&snapshot, next) |> ignore
#if DEBUG
                        Logger.LogFile [ "Refreshed snippets." ]
#endif
                    with
                    | :? OperationCanceledException
                    | :? ObjectDisposedException ->
#if DEBUG
                        Logger.LogFile [ $"Operation canceled or object disposed while refreshing snippets." ]
#else
                        ()
#endif
                    | e ->
#if DEBUG
                        Logger.LogFile [ $"Unexpected error occurred while refreshing snippets: {e.Message}" ]
#else
                        ()
#endif
                finally
                    if acquired then
                        try
                            semaphore.Release() |> ignore
                        with :? ObjectDisposedException ->
#if DEBUG
                            Logger.LogFile [ $"Semaphore was disposed while releasing." ]
#else
                            ()
#endif

            }
            |> ignore

        let refreshDebounceMs = 200
        let mutable pendingRefreshPath = ""

        let createGuardedTimer (invoke: unit -> unit) =
            new Timer(
                TimerCallback(fun _ ->
                    try
                        disposed.IfNotDisposed invoke
                    with
                    | :? ObjectDisposedException
                    | :? OperationCanceledException -> ()
                    | e ->
#if DEBUG
                        Logger.LogFile
                            [
                                $"Unexpected error occurred while running guarded timer callback: {e.Message}"
                            ]
#else
                        ()
#endif
                ),
                null,
                Timeout.Infinite,
                Timeout.Infinite
            )

        let refreshTimer =
            createGuardedTimer (fun () ->
                let path = Volatile.Read(&pendingRefreshPath)

                if not (String.IsNullOrWhiteSpace(path)) then
                    __.OnRefresh path
                    startRefreshTask path)

        let scheduleDebouncedRefresh (path: string) =
            Volatile.Write(&pendingRefreshPath, path)

            try
                refreshTimer.Change(refreshDebounceMs, Timeout.Infinite) |> ignore
            with :? ObjectDisposedException ->
                ()

        let watcherRestartBackoffMs = 200
        let mutable pendingRestartDirectory = ""

        let mutable restartAction: string -> unit = ignore

        let restartWatcherTimer =
            createGuardedTimer (fun () ->
                let path = Volatile.Read(&pendingRestartDirectory)

                if not (String.IsNullOrWhiteSpace(path)) then
                    restartAction path)

        let scheduleWatcherRestart (directory: string) =
            Volatile.Write(&pendingRestartDirectory, directory)

            try
                restartWatcherTimer.Change(watcherRestartBackoffMs, Timeout.Infinite) |> ignore
            with :? ObjectDisposedException ->
                ()

        let handleRefresh (e: FileSystemEventArgs) =
            disposed.IfNotDisposed(fun () ->
#if DEBUG
                Logger.LogFile
                    [
                        e.ChangeType.ToString(), sprintf "Snippets are refreshed due to file change: %s" e.FullPath
                    ]
#endif
                scheduleDebouncedRefresh e.FullPath)

        let rec startFileWatchingEvent (directory: string) =
            disposed.IfNotDisposed(fun () ->
                let w = __.CreateWatcher(directory, Config.snippetFilesName)

                w.EnableRaisingEvents <- true
                w.IncludeSubdirectories <- false
                w.NotifyFilter <- NotifyFilters.LastWrite

                handleRefresh |> w.Created.Add
                handleRefresh |> w.Changed.Add

                w.Error.Add
                <| fun e ->
#if DEBUG
                    Logger.LogFile [ $"Error occurred in file watching event: {e.GetException().Message}" ]
#endif
                    // NOTE: Only the currently registered watcher instance may restart.
                    if Object.ReferenceEquals(Volatile.Read(&watcher), w) then
                        if tryRemoveCurrentWatcher w then
                            scheduleWatcherRestart directory

                if disposed.IsDisposed then
                    // NOTE: Dispose immediately if already disposed.
                    disposeWatcher w
                else
                    exchangeAndDisposeWatcher w
#if DEBUG
                Logger.LogFile [ "Started file watching event." ]
#endif
            )

        do
            // NOTE: Assign after definition to avoid forward-reference issues.
            restartAction <- startFileWatchingEvent

        let snippetToSuggestion (s: SnippetEntry) =
            s.Group
            |> function
                | null -> PredictiveSuggestion(s.Snippet, s.Tooltip)
                | g -> PredictiveSuggestion(s.Snippet, $"[{g}]{s.Tooltip}")

        let (|Empty|_|) = String.IsNullOrWhiteSpace

        let inputPattern = Regex("^\\s*:([a-zA-Z0-9]+)(\\s*)(.*)")

        let (|Prefix|_|) (value: string) =
            // NOTE: Remove the snippet or tooltip symbol from the input.
            // NOTE: These symbols are used to exclude other predictors from suggestions.
            let m = inputPattern.Match(value)

            if m.Success then
                (m.Groups[1].Value, m.Groups[3].Value.TrimEnd(), m.Groups[2].Length > 0) |> Some
            else
                None

        let (|NoPrefix|) (value: string) = value.Trim()

        let isWordCharacter (text: string) index =
            let character = text[index]

            // NOTE: U+0000 through U+007F covers all ASCII characters, including controls and punctuation.
            // NOTE: ASCII letters, digits, and '_' match the Unicode word categories below without a category lookup.
            if character <= '\u007F' then
                (character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z')
                || (character >= '0' && character <= '9')
                || character = '_'
            else
                let index =
                    if
                        index > 0
                        && Char.IsLowSurrogate(character)
                        && Char.IsHighSurrogate(text[index - 1])
                    then
                        index - 1
                    else
                        index

                // NOTE: Letters, decimal digits, combining marks, and connector punctuation remain part of a word.
                // NOTE: Combining marks and connectors must not create word-start matches; other categories are delimiters.
                match CharUnicodeInfo.GetUnicodeCategory(text, index) with
                | UnicodeCategory.UppercaseLetter
                | UnicodeCategory.LowercaseLetter
                | UnicodeCategory.TitlecaseLetter
                | UnicodeCategory.ModifierLetter
                | UnicodeCategory.OtherLetter
                | UnicodeCategory.DecimalDigitNumber
                | UnicodeCategory.NonSpacingMark
                | UnicodeCategory.SpacingCombiningMark
                | UnicodeCategory.EnclosingMark
                | UnicodeCategory.ConnectorPunctuation -> true
                | _ -> false

        let isAsciiQuery (query: string) =
            let mutable asciiQuery = true
            let mutable position = 0

            while position < query.Length && asciiQuery do
                asciiQuery <- query[position] <= '\u007F'
                position <- position + 1

            asciiQuery

        let alternateQueryInitial comparison initial =
            if comparison = StringComparison.OrdinalIgnoreCase then
                if initial >= 'a' && initial <= 'z' then
                    char (int initial - 32)
                elif initial >= 'A' && initial <= 'Z' then
                    char (int initial + 32)
                else
                    initial
            else
                initial

        let hasAsciiWordStart comparison alternateInitial (query: string) (text: string) first =
            let initial = query[0]
            let querySpan = query.AsSpan()
            let last = text.Length - query.Length
            let mutable position = first + 1
            let mutable wordStart = false

            while position <= last && not wordStart do
                let candidate = text[position]

                if
                    (candidate = initial || candidate = alternateInitial || candidate > '\u007F')
                    && not (isWordCharacter text (position - 1))
                then
                    wordStart <- MemoryExtensions.StartsWith(text.AsSpan(position), querySpan, comparison)

                position <- position + 1

            wordStart

        let hasLaterWordStart (comparison: StringComparison) (query: string) (text: string) first =
            let mutable position = first
            let mutable wordStart = false

            while position >= 0 && not wordStart do
                position <- text.IndexOf(query, position + 1, comparison)

                if position >= 0 then
                    wordStart <- not (isWordCharacter text (position - 1))

            wordStart

        let matchRank (comparison: StringComparison) asciiQuery alternateInitial (query: string) (text: string) =
            let first = text.IndexOf(query, comparison)

            if first < 0 then
                ValueNone
            elif first = 0 then
                ValueSome(
                    if text.Length = query.Length then
                        ExactMatchRank
                    else
                        PrefixMatchRank
                )
            else
                let wordStart =
                    if not (isWordCharacter text (first - 1)) then
                        true
                    elif asciiQuery then
                        hasAsciiWordStart comparison alternateInitial query text first
                    else
                        hasLaterWordStart comparison query text first

                ValueSome(if wordStart then WordStartMatchRank else SubstringMatchRank)

        let reserveSuggestions (suggestions: Generic.List<PredictiveSuggestion>) count =
            if suggestions.Capacity < suggestions.Count + count then
                suggestions.Capacity <- suggestions.Count + count

        let appendUnrankedSnippets (current: Snapshot) pred (suggestions: Generic.List<PredictiveSuggestion>) =
            let mutable count = 0

            for snippet in current.Snippets do
                if pred snippet then
                    count <- count + 1

            reserveSuggestions suggestions count

            for snippet in current.Snippets do
                if pred snippet then
                    snippet |> snippetToSuggestion |> suggestions.Add

        let collectMatches (current: Snapshot) pred field (query: string) =
            let asciiQuery = isAsciiQuery query
            let alternateInitial = alternateQueryInitial current.SearchComparison query[0]

            let mutable matches: struct (int * int) array = Array.empty
            let mutable count = 0
            let mutable limit = 0
            let mutable ranksPresent = 0

            for index = 0 to current.Snippets.Length - 1 do
                let snippet = current.Snippets[index]

                if pred snippet then
                    match
                        field snippet
                        |> matchRank current.SearchComparison asciiQuery alternateInitial query
                    with
                    | ValueSome rank ->
                        if count = 0 then
                            limit <- current.Snippets.Length - index
                            matches <- Array.zeroCreate (min 4 limit)
                        elif count = matches.Length then
                            let capacity = count + min count (limit - count)
                            let expanded = Array.zeroCreate capacity
                            Array.Copy(matches, expanded, count)
                            matches <- expanded

                        matches[count] <- struct (index, rank)
                        count <- count + 1
                        ranksPresent <- ranksPresent ||| (1 <<< rank)
                    | ValueNone -> ()

            struct (matches, count, ranksPresent)

        let appendRankedSnippets
            (current: Snapshot)
            (suggestions: Generic.List<PredictiveSuggestion>)
            (matches: struct (int * int) array)
            count
            ranksPresent
            =
            reserveSuggestions suggestions count

            for rank = ExactMatchRank to SubstringMatchRank do
                if ranksPresent &&& (1 <<< rank) <> 0 then
                    for position = 0 to count - 1 do
                        let struct (index, candidateRank) = matches[position]

                        if candidateRank = rank then
                            current.Snippets[index] |> snippetToSuggestion |> suggestions.Add

        let chooseSnippets (current: Snapshot) pred field (suggestions: Generic.List<PredictiveSuggestion>) query =
            if String.IsNullOrEmpty(query) then
                appendUnrankedSnippets current pred suggestions
            else
                let struct (matches, count, ranksPresent) = collectMatches current pred field query
                appendRankedSnippets current suggestions matches count ranksPresent

            suggestions

        let chooseCompletionTexts (current: Snapshot) pred =
            current.Snippets
            |> Seq.choose (fun x -> if pred x then Some x.Snippet else None)
            |> Seq.toArray

        let completionIdentifierPattern = Regex("^\\s*:([a-zA-Z0-9]*)$")

        let (|CompletionIdentifier|_|) (value: string) =
            let m = completionIdentifierPattern.Match(value)

            if m.Success then Some m.Groups[1].Value else None

        let basicGroupIds = [| Snp; Tip |]

        let isKnownGroupIdOrPrefix (current: Snapshot) input =
            current.GroupIds
            |> Seq.append basicGroupIds
            |> Seq.exists (fun groupId -> groupId.StartsWith(input, StringComparison.Ordinal))

        let chooseGroupIds (current: Snapshot) input =
            current.GroupIds
            |> Seq.append basicGroupIds
            |> Seq.choose (fun groupId ->
                if groupId <> input && groupId.StartsWith(input) then
                    ($":{groupId}", "") |> PredictiveSuggestion |> Some
                else
                    None)

        let chooseCompletionGroupIds (current: Snapshot) input =
            let prefix = $":{input}"

            current.CompletionIdentifiers
            |> Array.filter (fun identifier -> identifier.StartsWith(prefix, StringComparison.Ordinal))

        abstract CreateWatcher: directory: string * filter: string -> FileSystemWatcher

        default _.CreateWatcher(directory: string, filter: string) =
            new FileSystemWatcher(directory, filter)

        abstract OnRefresh: path: string -> unit
        default _.OnRefresh(_path: string) = ()

        member __.load getSnippetPath =
            let snippetDirectory, snippetPath = getSnippetPath ()
            startRefreshTask snippetPath
            startFileWatchingEvent snippetDirectory

        member __.getPredictiveSuggestions(input: string) : Generic.List<PredictiveSuggestion> =
            let current = Volatile.Read(&snapshot)

            match input with
            | Empty -> Generic.List<PredictiveSuggestion>()
            | Prefix(groupId, input, hasSeparator) ->
#if DEBUG
                Logger.LogFile [ $"group:'{groupId}' input: '{input}'" ]
#endif

                let pred, field =
                    match groupId with
                    | Snp -> (fun _ -> true), (fun (snippet: SnippetEntry) -> snippet.Snippet)
                    | Tip -> (fun _ -> true), (fun snippet -> snippet.Tooltip)
                    | groupId -> (fun snippet -> snippet.Group = groupId), (fun snippet -> snippet.Snippet)

                let suggestions = Generic.List<PredictiveSuggestion>()

                if not hasSeparator && String.IsNullOrWhiteSpace(input) then
                    chooseGroupIds current groupId |> Seq.iter suggestions.Add

                input |> chooseSnippets current pred field suggestions
            | NoPrefix input ->
                input
                |> chooseSnippets current (fun _ -> true) _.Snippet (Generic.List<PredictiveSuggestion>())

        member __.getCompletionTexts(input: string) =
            let current = Volatile.Read(&snapshot)
            let comparisonType = current.SearchComparison

            match input with
            | Prefix(Snp, input, _) ->
                (fun (snippet: SnippetEntry) -> snippet.Snippet.Contains(input, comparisonType))
                |> chooseCompletionTexts current
            | Prefix(Tip, _, _) -> Array.empty
            | Prefix(groupId, input, _) when current.Groups.Contains groupId ->
                (fun (snippet: SnippetEntry) ->
                    snippet.Group = groupId && snippet.Snippet.Contains(input, comparisonType))
                |> chooseCompletionTexts current
            | CompletionIdentifier groupId -> chooseCompletionGroupIds current groupId
            | _ -> Array.empty

        member __.getExactIdentifierSnippetTexts(input: string) =
            let current = Volatile.Read(&snapshot)

            if current.HasValidConfiguration then
                match input with
                | Prefix(Snp, input, false) when String.IsNullOrEmpty(input) ->
                    (fun _ -> true) |> chooseCompletionTexts current
                | Prefix(Tip, _, false) -> Array.empty
                | Prefix(groupId, input, false) when String.IsNullOrEmpty(input) && current.Groups.Contains groupId ->
                    (fun snippet -> snippet.Group = groupId) |> chooseCompletionTexts current
                | _ -> Array.empty
            else
                Array.empty

        member __.isUnknownGroupIdentifier(input: string) =
            let current = Volatile.Read(&snapshot)

            if current.HasValidConfiguration then
                match input with
                | CompletionIdentifier groupId when not (String.IsNullOrEmpty(groupId)) ->
                    not (isKnownGroupIdOrPrefix current groupId)
                | _ -> false
            else
                false

        interface IDisposable with
            member __.Dispose() =
                if disposed.TryMarkDisposed() then
                    refreshCts.Cancel()

                    refreshTimer.Dispose()
                    restartWatcherTimer.Dispose()

                    exchangeAndDisposeWatcher null
                    refreshCts.Dispose()
                    semaphore.Dispose()
