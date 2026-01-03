namespace SnippetPredictor

open System
open System.IO
open System.Text.RegularExpressions

module Snippet =
    [<Literal>]
    let name = "Snippet"

    open System.Collections
    open System.Management.Automation
    open System.Management.Automation.Subsystem.Prediction
    open System.Threading
    open File

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

    type Cache() as __ =

        let mutable caseSensitive = CaseSensitivity.insensitive
        let snippets = Concurrent.ConcurrentQueue<SnippetEntry>()
        let groups = new Concurrent.ConcurrentDictionary<string, unit>()
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
                        snippets.Clear()
                        groups.Clear()

                        result
                        |> function
                            | ConfigState.Empty -> ()
                            | ConfigState.Valid { SearchCaseSensitive = searchCaseSensitive
                                                  Snippets = snps } ->
                                Interlocked.Exchange(
                                    &caseSensitive,
                                    searchCaseSensitive |> SearchCaseSensitivity.ofBool
                                )
                                |> ignore

                                snps
                                |> function
                                    | null -> Array.empty
                                    | snippets -> snippets
                                |> Array.iter (fun s ->
                                    snippets.Enqueue s

                                    match s.Group with
                                    | null -> ()
                                    | g ->
                                        if g |> groups.ContainsKey |> not then
                                            groups.TryAdd(g, ()) |> ignore)
                            | ConfigState.Invalid record -> record |> snippets.Enqueue
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
                            [ $"Unexpected error occurred while running guarded timer callback: {e.Message}" ]
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
                    [ e.ChangeType.ToString(), sprintf "Snippets are refreshed due to file change: %s" e.FullPath ]
#endif
                scheduleDebouncedRefresh e.FullPath)

        let rec startFileWatchingEvent (directory: string) =
            disposed.IfNotDisposed(fun () ->
                let w = __.CreateWatcher(directory, File.snippetFilesName)

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

        let snippetToTuple (s: SnippetEntry) =
            s.Group
            |> function
                | null -> s.Snippet, s.Tooltip
                | g -> s.Snippet, $"[{g}]{s.Tooltip}"

        let (|Empty|_|) = String.IsNullOrWhiteSpace

        let inputPattern = Regex("^\\s*:([a-zA-Z0-9]+)\\s*(.*)")

        let (|Prefix|_|) (value: string) =
            // NOTE: Remove the snippet or tooltip symbol from the input.
            // NOTE: These symbols are used to exclude other predictors from suggestions.
            let m = inputPattern.Match(value)

            m.Groups.Count
            |> function
                | 2 -> (m.Groups[1].Value, "") |> Some
                | 3 -> (m.Groups[1].Value, m.Groups[2].Value.TrimEnd()) |> Some
                | _ -> None

        let (|NoPrefix|) (value: string) = value.Trim()

        let chooseSnippets pred =
            snippets
            |> Seq.choose (fun x ->
                if pred x then
                    Some(snippetToTuple x |> PredictiveSuggestion)
                else
                    None)

        [<Literal>]
        let Snp = "snp"

        [<Literal>]
        let Tip = "tip"

        let basicGroupIds = [| Snp; Tip |]

        let chooseGroupIds input =
            groups.Keys
            |> Seq.append basicGroupIds
            |> Seq.choose (fun groupId ->
                if groupId <> input && groupId.StartsWith(input) then
                    ($":{groupId}", "") |> PredictiveSuggestion |> Some
                else
                    None)

        abstract CreateWatcher: directory: string * filter: string -> FileSystemWatcher

        default _.CreateWatcher(directory: string, filter: string) =
            new FileSystemWatcher(directory, filter)

        abstract OnRefresh: path: string -> unit
        default _.OnRefresh(_path: string) = ()

        member __.load getSnippetPath =
            let snippetDirectory, snippetPath = getSnippetPath ()

            if File.Exists(snippetPath) then
                startRefreshTask snippetPath

            startFileWatchingEvent snippetDirectory

        member __.getPredictiveSuggestions(input: string) : Generic.List<PredictiveSuggestion> =
            let comparisonType = caseSensitive |> SearchCaseSensitivity.stringComparison

            match input with
            | Empty -> Seq.empty
            | Prefix(groupId, input) ->
#if DEBUG
                Logger.LogFile [ $"group:'{groupId}' input: '{input}'" ]
#endif

                let pred =
                    match groupId with
                    | Snp -> _.Snippet.Contains(input, comparisonType)
                    | Tip -> _.Tooltip.Contains(input, comparisonType)
                    | groupId -> fun (s: SnippetEntry) -> s.Group = groupId && s.Snippet.Contains(input, comparisonType)

                let groupIds =
                    if String.IsNullOrWhiteSpace(input) then
                        chooseGroupIds groupId
                    else
                        Seq.empty

                pred |> chooseSnippets |> Seq.append groupIds
            | NoPrefix input -> _.Snippet.Contains(input, comparisonType) |> chooseSnippets
            |> Linq.Enumerable.ToList

        interface IDisposable with
            member __.Dispose() =
                if disposed.TryMarkDisposed() then
                    refreshCts.Cancel()

                    refreshTimer.Dispose()
                    restartWatcherTimer.Dispose()

                    exchangeAndDisposeWatcher null
                    refreshCts.Dispose()
                    semaphore.Dispose()

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

    let toError (e: SnippetEntry) =
        if String.IsNullOrEmpty e.Tooltip then
            e.Snippet
        else
            $"{e.Snippet}: {e.Tooltip}"
        |> Error

    let loadConfig (getSnippetPath: unit -> string * string) =
        let snippetPath = getSnippetPath () |> snd

        if snippetPath |> (File.Exists >> not) then
            ConfigState.Empty
        else
            snippetPath |> parseSnippetFile |> _.Result

    let makeErrorRecord (e: string) =
        new ErrorRecord(new Exception(e), "", ErrorCategory.InvalidData, null)

    let makeSnippetEntry (snippet: string) (tooltip: string) (group: string | null) =
        { Snippet = snippet
          Tooltip = tooltip
          Group = group }

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

    let addSnippets getSnippetPath (snippets: SnippetEntry seq) =
        loadConfig getSnippetPath
        |> function
            | ConfigState.Empty ->
                { SearchCaseSensitive = false
                  Snippets = Array.ofSeq snippets }
                |> storeConfig getSnippetPath
            | ConfigState.Valid config ->
                let newSnippets =
                    config.Snippets
                    |> function
                        | null -> Array.ofSeq snippets
                        | snps -> Array.append snps <| Array.ofSeq snippets

                { config with Snippets = newSnippets } |> storeConfig getSnippetPath
            | ConfigState.Invalid e -> e |> toError

    let removeSnippets getSnippetPath (snippets: string seq) =
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
