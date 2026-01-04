module SnippetPredictorTest.Snippet

open Expecto
open Expecto.Flip

open SnippetPredictor
open SnippetPredictorTest.Utility

open System
open System.Diagnostics
open System.IO

[<Tests>]
let tests_Dispose =
    testList
        "Nullable.dispose"
        [

          test "when value is null" {
              // NOTE: for coverage.
              null |> Nullable.dispose
          }

          ]

#if DEBUG
[<Tests>]
let tests_Disposal =
    // NOTE: for coverage.
    testList
        "Disposal"
        [

          test "when not disposed" {
              let flag = Snippet.Disposal.Flag()
              let mutable called = false
              flag.IfDisposed(fun () -> failtest "should not call the function disposed handler")
              flag.IfNotDisposed(fun () -> called <- true)
              Expect.equal "should call the function not disposed handler" called true
          }

          test "when disposed" {
              let flag = Snippet.Disposal.Flag()
              let mutable called = false
              flag.TryMarkDisposed() |> ignore
              flag.IfDisposed(fun () -> called <- true)
              flag.IfNotDisposed(fun () -> failtest "should not call the function not disposed handler")
              Expect.equal "should call the function disposed handler" called true
          }

          ]
#endif

[<Tests>]
let tests_parseSnippets =
    let expectValid =
        function
        | File.ConfigState.Valid entry -> entry
        | _ -> failtest "Expected ConfigState.Valid but got a different state"

    let expectInvalid =
        function
        | File.ConfigState.Invalid entry -> entry
        | _ -> failtest "Expected ConfigState.Invalid but got a different state"

    testList
        "parseSnippets"
        [

          test "when JSON is empty string" {
              ""
              |> File.parseSnippets
              |> _.IsEmpty
              |> Expect.equal "should return ConfigState.Empty" true
          }

          test "when JSON is null" {
              "null"
              |> File.parseSnippets
              |> expectInvalid
              |> Expect.equal
                  "should return ConfigState.Invalid"
                  { SnippetEntry.Snippet = "'.snippet-predictor.json is null or invalid format.'"
                    SnippetEntry.Tooltip = ""
                    SnippetEntry.Group = null }
          }

          test "when JSON is empty" {
              "{}"
              |> File.parseSnippets
              |> expectValid
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SearchCaseSensitive = false
                    SnippetConfig.Snippets = null }
          }

          test "when JSON is broken" {
              "{"
              |> File.parseSnippets
              |> expectInvalid
              |> Expect.equal
                  "should return ConfigState.Invalid"
                  { SnippetEntry.Snippet = "'An error occurred while parsing .snippet-predictor.json'"
                    SnippetEntry.Tooltip =
                      "Expected depth to be zero at the end of the JSON payload. There is an open JSON object or array that should be closed. Path: $ | LineNumber: 0 | BytePositionInLine: 1."
                    SnippetEntry.Group = null }

          }

          test "when JSON has null snippets" {
              """{"snippets":null}"""
              |> File.parseSnippets
              |> expectValid
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SearchCaseSensitive = false
                    SnippetConfig.Snippets = null }
          }

          test "when JSON has empty snippets" {
              """{"snippets":[]}"""
              |> File.parseSnippets
              |> expectValid
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SearchCaseSensitive = false
                    SnippetConfig.Snippets = [||] }
          }

          test "when JSON has snippets without group" {
              """{"snippets":[{"snippet": "echo 'example'", "tooltip": "example tooltip"}]}"""
              |> File.parseSnippets
              |> expectValid
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SearchCaseSensitive = false
                    Snippets =
                      [| { SnippetEntry.Snippet = "echo 'example'"
                           SnippetEntry.Tooltip = "example tooltip"
                           SnippetEntry.Group = null } |] }
          }

          test "when JSON has snippets" {
              """{"snippets":[{"snippet": "echo 'example'", "tooltip": "example tooltip", "group": "group"}]}"""
              |> File.parseSnippets
              |> expectValid
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SearchCaseSensitive = false
                    SnippetConfig.Snippets =
                      [| { SnippetEntry.Snippet = "echo 'example'"
                           SnippetEntry.Tooltip = "example tooltip"
                           SnippetEntry.Group = "group" } |] }
          }

          test "when JSON has snippets with trailing comma" {
              """{
    // comment
    "snippets":[
        {"snippet": "echo 'example'", "tooltip": "example tooltip"},
    ]
}"""
              |> File.parseSnippets
              |> expectValid
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SearchCaseSensitive = false
                    SnippetConfig.Snippets =
                      [| { SnippetEntry.Snippet = "echo 'example'"
                           SnippetEntry.Tooltip = "example tooltip"
                           SnippetEntry.Group = null } |] }
          }

          test "when JSON has snippet that has null group" {
              """{"snippets":[{"snippet": "echo 'example'", "tooltip": "example tooltip", "group": null}]}"""
              |> File.parseSnippets
              |> expectValid
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SearchCaseSensitive = false
                    SnippetConfig.Snippets =
                      [| { SnippetEntry.Snippet = "echo 'example'"
                           SnippetEntry.Tooltip = "example tooltip"
                           SnippetEntry.Group = null } |] }
          }

          test "when JSON has snippet that has group with disallowed characters" {
              """{"snippets":[{"snippet": "echo 'example'", "tooltip": "example tooltip", "group": "group!"}]}"""
              |> File.parseSnippets
              |> expectInvalid
              |> Expect.equal
                  "should return ConfigState.Invalid"
                  { SnippetEntry.Snippet = "'An error occurred while parsing .snippet-predictor.json'"
                    SnippetEntry.Tooltip = "Invalid characters in group: group!"
                    SnippetEntry.Group = null }
          }

          test "when JSON has search case sensitive set to true" {
              """{"searchCaseSensitive": true, "snippets":[{"snippet": "echo 'example'", "tooltip": "example tooltip", "group": null}]}"""
              |> File.parseSnippets
              |> expectValid
              |> Expect.equal
                  "should return SearchCaseSensitive"
                  { SearchCaseSensitive = true
                    SnippetConfig.Snippets =
                      [| { SnippetEntry.Snippet = "echo 'example'"
                           SnippetEntry.Tooltip = "example tooltip"
                           SnippetEntry.Group = null } |] }
          }
          test "when JSON has search case sensitive set to null" {
              """{"searchCaseSensitive": null, "snippets":[{"snippet": "echo 'example'", "tooltip": "example tooltip", "group": null}]}"""
              |> File.parseSnippets
              |> expectValid
              |> Expect.equal
                  "should return SearchCaseSensitive"
                  { SearchCaseSensitive = false
                    SnippetConfig.Snippets =
                      [| { SnippetEntry.Snippet = "echo 'example'"
                           SnippetEntry.Tooltip = "example tooltip"
                           SnippetEntry.Group = null } |] }
          }

          ]

module getSnippet =

    let PathSeparator = Path.DirectorySeparatorChar

    [<Tests>]
    let tests_getSnippetPathWith =
        testList
            "getSnippetPathWith"
            [

              test "when env var is set" {
                  File.getSnippetPathWith (fun _ -> ".") (fun _ -> "")
                  |> Expect.equal
                      "should return the path based on env var."
                      (".", $".{PathSeparator}.snippet-predictor.json")
              }

              test "when env var is null" {
                  let userProfile = "/Users/username"

                  File.getSnippetPathWith (fun _ -> null) (fun _ -> userProfile)
                  |> Expect.equal
                      "should return the default path"
                      (userProfile, $"{userProfile}{PathSeparator}.snippet-predictor.json")
              }

              test "when env var is empty" {
                  let userProfile = "/Users/username"

                  File.getSnippetPathWith (fun _ -> "") (fun _ -> userProfile)
                  |> Expect.equal
                      "should return the default path"
                      (userProfile, $"{userProfile}{PathSeparator}.snippet-predictor.json")
              }

              ]

module getPredictiveSuggestions =
    open System.Management.Automation.Subsystem.Prediction

    [<Tests>]
    let tests_getPredictiveSuggestions =
        let cache = new Snippet.Cache()
        cache.load (fun () -> "./", "./.snippet-predictor-valid.json")

        let expected1 =
            { SnippetEntry.Snippet = "echo 'example'"
              SnippetEntry.Tooltip = "example  tooltip"
              SnippetEntry.Group = "group" }

        let expected2 =
            { SnippetEntry.Snippet = "touch sample.txt"
              SnippetEntry.Tooltip = "new file"
              SnippetEntry.Group = null }

        let entryToSuggestion snippet =
            PredictiveSuggestion(
                snippet.Snippet,
                match snippet.Group with
                | null -> snippet.Tooltip
                | _ -> $"[{snippet.Group}]{snippet.Tooltip}"
            )

        let asserter (expected: PredictiveSuggestion) (actual: PredictiveSuggestion) =
            actual.SuggestionText = expected.SuggestionText
            && actual.ToolTip = expected.ToolTip

        testList
            "getPredictiveSuggestions"
            [

              test "when snippet symbol is set and matched" {
                  let actual = cache.getPredictiveSuggestions ":snp      Echo    "
                  actual |> Expect.isNonEmpty "snippets"

                  actual
                  |> Expect.all
                      "should return the snippets filtered by the input removing snippet symbol."
                      (entryToSuggestion >> asserter <| expected1)
              }

              test "when snippet symbol is not set and matched" {
                  let actual = cache.getPredictiveSuggestions "    tou    "
                  actual |> Expect.isNonEmpty "snippets"

                  actual
                  |> Expect.all
                      "should return the snippets filtered by the input."
                      (entryToSuggestion >> asserter <| expected2)
              }

              test "when no snippets matched with :" {
                  cache.getPredictiveSuggestions ":    " |> Expect.isEmpty "should return empty."
              }

              test "when no snippets matched" {
                  cache.getPredictiveSuggestions "    exo    "
                  |> Expect.isEmpty "should return empty."
              }

              test "when input is whitespace" {
                  cache.getPredictiveSuggestions "    " |> Expect.isEmpty "should return empty."
              }

              test "when tooltip symbol is set and matched" {
                  let actual = cache.getPredictiveSuggestions ":tip    Example  tooltip    "
                  actual |> Expect.isNonEmpty "snippets"

                  actual
                  |> Expect.all
                      "should return the snippets filtered by the input removing tooltip symbol."
                      (entryToSuggestion >> asserter <| expected1)
              }

              test "when tooltip symbol is not set and not matched" {
                  cache.getPredictiveSuggestions "    example  tooltip    "
                  |> Expect.isEmpty "should return empty."
              }

              test "when group symbol is set and matched" {
                  let actual = cache.getPredictiveSuggestions ":group     "
                  actual |> Expect.isNonEmpty "snippets"

                  actual
                  |> Expect.all
                      "should return the snippets filtered by the input removing group symbol."
                      (entryToSuggestion >> asserter <| expected1)
              }

              test "when group symbol is set and matched case insensitive" {
                  let actual = cache.getPredictiveSuggestions ":group  Echo   "
                  actual |> Expect.isNonEmpty "snippets"

                  actual
                  |> Expect.all
                      "should return the snippets filtered by the input removing group symbol."
                      (entryToSuggestion >> asserter <| expected1)
              }

              test "when no group symbol is set and not matched" {
                  cache.getPredictiveSuggestions "    group    "
                  |> Expect.isEmpty "should return empty."
              }

              test "when group symbol is set and invalid" {
                  cache.getPredictiveSuggestions ":grp     "
                  |> Expect.isEmpty "should return empty."
              }

              let expectedGroups =
                  [ PredictiveSuggestion(":group", "")
                    PredictiveSuggestion("Write-Host gr", "[gr]example 2") ]

              test "when group symbol is set and partially matched" {
                  cache.getPredictiveSuggestions "   :gr     "
                  |> Seq.iteri (fun index actual ->
                      actual
                      |> asserter expectedGroups[index]
                      |> Expect.isTrue "should return group and matched snippets")
              }

              test "when group symbol is set and start non-whitespace and partially matched" {
                  cache.getPredictiveSuggestions "  x :gr     "
                  |> Expect.isEmpty "should return empty."
              }

              ]

    [<Tests>]
    let tests_Dispose =
        let cache = new Snippet.Cache()

        testList
            "Cache.Dispose"
            [

              test "OnRefresh default is callable" {
                  // NOTE: for coverage. (Cache.OnRefresh has a default implementation.)
                  cache.OnRefresh("dummy")
              }

              test "when watcher is stopped" {
                  // NOTE: for coverage.
                  (cache :> IDisposable).Dispose()
              }

              ]

module CacheDisposeBehavior =

    let waitUntil (timeoutMs: int) (pollMs: int) (predicate: unit -> bool) =
        let sw = Stopwatch.StartNew()

        while sw.ElapsedMilliseconds < int64 timeoutMs && not (predicate ()) do
            System.Threading.Thread.Sleep pollMs

        predicate ()

    let waitUntilFileUnlocked (timeoutMs: int) (pollMs: int) (filePath: string) =
        waitUntil timeoutMs pollMs (fun () ->
            try
                use _ = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)

                true
            with :? IOException ->
                false)

    type CacheForTest(createWatcher: string * string -> FileSystemWatcher, onRefresh: string -> unit) =
        inherit Snippet.Cache()

        override _.CreateWatcher(directory: string, filter: string) = createWatcher (directory, filter)

        override _.OnRefresh(path: string) = onRefresh path

    type TestWatcher(directory: string, filter: string) =
        inherit FileSystemWatcher(directory, filter)

        // NOTE: For this behavior test we need to be able to raise events after Cache.Dispose() without relying on OS timing. Avoid tearing down native resources here.
        override __.Dispose(_: bool) = __.EnableRaisingEvents <- false

        member __.ReleaseHandles() = base.Dispose(true)

        member __.TriggerChanged(directory: string, name: string) =
            __.OnChanged(new FileSystemEventArgs(WatcherChangeTypes.Changed, directory, name))

        member __.TriggerError(ex: exn) = __.OnError(new ErrorEventArgs(ex))

    [<Tests>]
    let tests_DisposeStopsHandlingEvents =
        testList
            "Cache.Dispose behavior"
            [ test "Changed after Dispose does nothing" {
                  use tmpDir = new TempDirectory("SnippetPredictor.Test.")
                  let fileName = ".snippet-predictor.json"
                  let filePath = Path.Combine(tmpDir.Path, fileName)
                  File.WriteAllText(filePath, """{"Snippets": []}""")

                  let mutable watcherCreatedCount = 0
                  let mutable refreshCalls = 0
                  let mutable watcher: TestWatcher option = None

                  let cache =
                      new CacheForTest(
                          (fun _ ->
                              watcherCreatedCount <- watcherCreatedCount + 1
                              let w = new TestWatcher(tmpDir.Path, fileName)
                              watcher <- Some w
                              w),
                          (fun _ -> refreshCalls <- refreshCalls + 1)
                      )

                  cache.load (fun () -> tmpDir.Path, filePath)
                  (cache :> IDisposable).Dispose()

                  let w = watcher |> Expect.wantSome "watcher should be created"

                  try
                      w.TriggerChanged(tmpDir.Path, fileName)
                  finally
                      w.ReleaseHandles()

                  waitUntilFileUnlocked 2000 20 filePath
                  |> Expect.isTrue "temp snippet file should be unlocked after Dispose"

                  refreshCalls |> Expect.equal "should not refresh after Dispose" 0

                  watcherCreatedCount |> Expect.equal "should not create watcher again" 1
              }

              test "Error after Dispose does not restart watcher" {
                  use tmpDir = new TempDirectory("SnippetPredictor.Test.")
                  let fileName = ".snippet-predictor.json"
                  let filePath = Path.Combine(tmpDir.Path, fileName)
                  File.WriteAllText(filePath, """{"Snippets": []}""")

                  let mutable watcherCreatedCount = 0
                  let mutable watcher: TestWatcher option = None

                  let cache =
                      new CacheForTest(
                          (fun _ ->
                              watcherCreatedCount <- watcherCreatedCount + 1
                              let w = new TestWatcher(tmpDir.Path, fileName)
                              watcher <- Some w
                              w),
                          ignore
                      )

                  cache.load (fun () -> tmpDir.Path, filePath)
                  (cache :> IDisposable).Dispose()

                  let w = watcher |> Expect.wantSome "watcher should be created"

                  try
                      w.TriggerError(InvalidOperationException("boom"))
                  finally
                      w.ReleaseHandles()

                  waitUntilFileUnlocked 2000 20 filePath
                  |> Expect.isTrue "temp snippet file should be unlocked after Dispose"

                  watcherCreatedCount |> Expect.equal "should not restart watcher after Dispose" 1
              } ]

    [<Tests>]
    let tests_ErrorBackoff =
        testList
            "Cache watcher restart backoff"
            [

              test "Error restarts watcher after backoff (not immediately)" {
                  use tmpDir = new TempDirectory("SnippetPredictor.Test.")
                  let fileName = ".snippet-predictor.json"
                  let filePath = Path.Combine(tmpDir.Path, fileName)
                  File.WriteAllText(filePath, """{"Snippets": []}""")

                  let mutable watcherCreatedCount = 0
                  let mutable watchers: TestWatcher list = []

                  let cache =
                      new CacheForTest(
                          (fun _ ->
                              watcherCreatedCount <- watcherCreatedCount + 1
                              let w = new TestWatcher(tmpDir.Path, fileName)
                              watchers <- w :: watchers
                              w),
                          ignore
                      )

                  try
                      cache.load (fun () -> tmpDir.Path, filePath)

                      watcherCreatedCount |> Expect.equal "should create initial watcher" 1
                      let w = watchers |> List.tryHead |> Expect.wantSome "watcher should be created"

                      let sw = Stopwatch.StartNew()
                      w.TriggerError(InvalidOperationException("boom"))

                      // Should not restart immediately.
                      System.Threading.Thread.Sleep 50
                      watcherCreatedCount |> Expect.equal "should not restart watcher immediately" 1

                      // Should restart after the fixed backoff.
                      waitUntil 2000 20 (fun () -> watcherCreatedCount = 2)
                      |> Expect.isTrue "should restart watcher after backoff"

                      (sw.ElapsedMilliseconds >= 150L)
                      |> Expect.isTrue "restart should be delayed (backoff)"
                  finally
                      (cache :> IDisposable).Dispose()

                      // Cleanup: release native handles for all watchers.
                      for w in watchers do
                          w.ReleaseHandles()

                      waitUntilFileUnlocked 2000 20 filePath
                      |> Expect.isTrue "temp snippet file should be unlocked after Dispose"
              }

              ]

    [<Tests>]
    let tests_ChangedIsDebounced =
        let testWithRelease (w: TestWatcher) (cache: Snippet.Cache) (filePath: string) (test: unit -> unit) =
            try
                test ()
            finally
                w.ReleaseHandles()
                (cache :> IDisposable).Dispose()

                waitUntilFileUnlocked 2000 20 filePath
                |> Expect.isTrue "temp snippet file should be unlocked after Cache.Dispose"

        let testTriggerAndRelease
            (w: TestWatcher)
            (cache: Snippet.Cache)
            (predicate: unit -> bool)
            (tmpDirPath: string)
            (fileName: string)
            (filePath: string)
            =
            testWithRelease w cache filePath (fun () ->
                w.TriggerChanged(tmpDirPath, fileName)

                waitUntil 2000 20 predicate
                |> Expect.isTrue "should reach OnRefresh even if it throws")

        testList
            "Cache debounced refresh"
            [

              test "Changed is debounced" {
                  use tmpDir = new TempDirectory("SnippetPredictor.Test.")
                  let fileName = ".snippet-predictor.json"
                  let filePath = Path.Combine(tmpDir.Path, fileName)
                  File.WriteAllText(filePath, """{"Snippets": []}""")

                  let mutable refreshCalls = 0
                  let mutable watcher: TestWatcher option = None

                  let cache =
                      new CacheForTest(
                          (fun _ ->
                              let w = new TestWatcher(tmpDir.Path, fileName)
                              watcher <- Some w
                              w),
                          (fun _ -> refreshCalls <- refreshCalls + 1)
                      )

                  cache.load (fun () -> tmpDir.Path, filePath)

                  refreshCalls <- 0

                  let w = watcher |> Expect.wantSome "watcher should be created"

                  testWithRelease w cache filePath (fun () ->
                      for _ in 1..10 do
                          w.TriggerChanged(tmpDir.Path, fileName)

                      waitUntil 2000 20 (fun () -> refreshCalls = 1)
                      |> Expect.isTrue "should refresh exactly once after debounced Changed burst"

                      System.Threading.Thread.Sleep 400
                      refreshCalls |> Expect.equal "should still be one refresh" 1)

              }

              test "Debounced callback swallows ObjectDisposedException" {
                  use tmpDir = new TempDirectory("SnippetPredictor.Test.")
                  let fileName = ".snippet-predictor.json"
                  let filePath = Path.Combine(tmpDir.Path, fileName)
                  File.WriteAllText(filePath, """{"Snippets": []}""")

                  let mutable called = false
                  let mutable watcher: TestWatcher option = None

                  let cache =
                      new CacheForTest(
                          (fun _ ->
                              let w = new TestWatcher(tmpDir.Path, fileName)
                              watcher <- Some w
                              w),
                          (fun _ ->
                              called <- true
                              raise (ObjectDisposedException("boom")))
                      )

                  cache.load (fun () -> tmpDir.Path, filePath)
                  let w = watcher |> Expect.wantSome "watcher should be created"
                  testTriggerAndRelease w cache (fun () -> called) tmpDir.Path fileName filePath
              }

              test "Debounced callback swallows OperationCanceledException" {
                  use tmpDir = new TempDirectory("SnippetPredictor.Test.")
                  let fileName = ".snippet-predictor.json"
                  let filePath = Path.Combine(tmpDir.Path, fileName)
                  File.WriteAllText(filePath, """{"Snippets": []}""")

                  let mutable called = false
                  let mutable watcher: TestWatcher option = None

                  let cache =
                      new CacheForTest(
                          (fun _ ->
                              let w = new TestWatcher(tmpDir.Path, fileName)
                              watcher <- Some w
                              w),
                          (fun _ ->
                              called <- true
                              raise (OperationCanceledException("boom")))
                      )

                  cache.load (fun () -> tmpDir.Path, filePath)
                  let w = watcher |> Expect.wantSome "watcher should be created"
                  testTriggerAndRelease w cache (fun () -> called) tmpDir.Path fileName filePath
              }

              test "Debounced callback swallows unexpected exceptions" {
                  use tmpDir = new TempDirectory("SnippetPredictor.Test.")
                  let fileName = ".snippet-predictor.json"
                  let filePath = Path.Combine(tmpDir.Path, fileName)
                  File.WriteAllText(filePath, """{"Snippets": []}""")

                  let mutable called = false
                  let mutable watcher: TestWatcher option = None

                  let cache =
                      new CacheForTest(
                          (fun _ ->
                              let w = new TestWatcher(tmpDir.Path, fileName)
                              watcher <- Some w
                              w),
                          (fun _ ->
                              called <- true
                              raise (InvalidOperationException("boom")))
                      )

                  cache.load (fun () -> tmpDir.Path, filePath)
                  let w = watcher |> Expect.wantSome "watcher should be created"
                  testTriggerAndRelease w cache (fun () -> called) tmpDir.Path fileName filePath
              }

              ]

[<Tests>]
let tests_loadSnippets =

    testList
        "loadSnippets"
        [

          test "when snippet file is not found" {
              Store.loadSnippets (fun () -> "./", "./not-found.json")
              |> Expect.wantOk "should return Ok"
              |> Expect.isEmpty "should return Empty"
          }

          test "when snippet file is invalid" {
              Store.loadSnippets (fun () -> "./", "./.snippet-predictor-invalid.json")
              |> Expect.wantError "should return Error"
              |> Expect.equal
                  "should return Error entry"
                  "'An error occurred while parsing .snippet-predictor.json': Expected depth to be zero at the end of the JSON payload. There is an open JSON object or array that should be closed. Path: $.Snippets[0] | LineNumber: 1 | BytePositionInLine: 15."
          }

          test "when snippet file is valid and null" {
              Store.loadSnippets (fun () -> "./", "./.snippet-predictor-null.json")
              |> Expect.wantError "should return Error"
              |> Expect.equal "should return Error entry" "'.snippet-predictor.json is null or invalid format.'"
          }

          test "when snippet file is valid and snippets is null" {
              Store.loadSnippets (fun () -> "./", "./.snippet-predictor-snippet-null.json")
              |> Expect.wantOk "should return Ok"
              |> Expect.isEmpty "should return Empty"
          }

          test "when snippet file is valid" {
              let expected =
                  [|

                     { SnippetEntry.Snippet = "echo 'example'"
                       SnippetEntry.Tooltip = "example  tooltip"
                       SnippetEntry.Group = "group" }
                     { SnippetEntry.Snippet = "touch sample.txt"
                       SnippetEntry.Tooltip = "new file"
                       SnippetEntry.Group = null }
                     { SnippetEntry.Snippet = "Write-Host gr"
                       SnippetEntry.Tooltip = "example 2"
                       SnippetEntry.Group = "gr" }

                     |]

              Store.loadSnippets (fun () -> "./", "./.snippet-predictor-valid.json")
              |> Expect.wantOk "should return Ok"
              |> Expect.equal "should return snippets" expected
          }

          ]

module addAndRemoveSnippets =

    [<Tests>]
    let tests_addSnippets =
        testList
            "addSnippets"
            [

              test "when snippet file directory is not found" {
                  let tmpDir =
                      Path.Combine(Path.GetTempPath(), $"SnippetPredictor.Test.{Guid.NewGuid().ToString()}")

                  let path = Path.Combine(tmpDir, "not-found.json")

                  [ { SnippetEntry.Snippet = "echo '1'"
                      SnippetEntry.Tooltip = "1 tooltip"
                      SnippetEntry.Group = null } ]
                  |> Store.addSnippets (fun () -> tmpDir, path)
                  |> Expect.wantError "should return Error"
                  |> Expect.equal "should return Error" $"Could not find a part of the path '{path}'."
              }

              test "when snippet file is not found" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, "not-found.json")

                  [ { SnippetEntry.Snippet = "echo '1'"
                      SnippetEntry.Tooltip = "1 tooltip"
                      SnippetEntry.Group = null } ]
                  |> Store.addSnippets (fun () -> tmp.Path, path)
                  |> Expect.wantOk "should return Ok"
                  |> Expect.equal "should return Ok" ()

                  let expected =
                      """{
  "SearchCaseSensitive": false,
  "Snippets": [
    {
      "Snippet": "echo '1'",
      "Tooltip": "1 tooltip"
    }
  ]
}"""
                      |> normalizeNewlines

                  File.ReadAllText(path)
                  |> normalizeNewlines
                  |> Expect.equal "should create the snippet file" expected
              }

              test "when snippet file is invalid" {
                  use tmp = new TempFile(".snippet-predictor-invalid.json", """{"Snippets":[}""")

                  [ { SnippetEntry.Snippet = "echo '2'"
                      SnippetEntry.Tooltip = "2 tooltip"
                      SnippetEntry.Group = null } ]
                  |> Store.addSnippets tmp.GetSnippetPath
                  |> Expect.wantError "should return Error"
                  |> Expect.equal
                      "should return Error entry"
                      "'An error occurred while parsing .snippet-predictor.json': '}' is an invalid start of a value. Path: $.Snippets[0] | LineNumber: 0 | BytePositionInLine: 13."
              }

              test "when snippet file is valid" {
                  use tmp = new TempFile(".snippet-predictor-valid.json", """{"Snippets": []}""")

                  [| { SnippetEntry.Snippet = "echo '3'"
                       SnippetEntry.Tooltip = "3 tooltip"
                       SnippetEntry.Group = null } |]
                  |> Store.addSnippets tmp.GetSnippetPath
                  |> Expect.wantOk "should return Ok"
                  |> Expect.equal "should return snippets" ()

                  let expected =
                      """{
  "SearchCaseSensitive": false,
  "Snippets": [
    {
      "Snippet": "echo '3'",
      "Tooltip": "3 tooltip"
    }
  ]
}"""
                      |> normalizeNewlines

                  tmp.GetSnippetContent()
                  |> Expect.equal "should add the snippet to snippet file" expected

              }

              test "when snippet file is valid and omitted group" {
                  use tmp = new TempFile(".snippet-predictor-valid.json", """{"Snippets": null}""")

                  [| { SnippetEntry.Snippet = "echo '3'"
                       SnippetEntry.Tooltip = "3 tooltip"
                       SnippetEntry.Group = null } |]
                  |> Store.addSnippets tmp.GetSnippetPath
                  |> Expect.wantOk "should return Ok"
                  |> Expect.equal "should return snippets" ()

                  let expected =
                      """{
  "SearchCaseSensitive": false,
  "Snippets": [
    {
      "Snippet": "echo '3'",
      "Tooltip": "3 tooltip"
    }
  ]
}"""
                      |> normalizeNewlines

                  tmp.GetSnippetContent()
                  |> Expect.equal "should add the snippet to snippet file" expected

              }

              test "when snippet file is valid and has group" {
                  use tmp = new TempFile(".snippet-predictor-valid.json", """{"Snippets": null}""")

                  [| { SnippetEntry.Snippet = "echo '4'"
                       SnippetEntry.Tooltip = "4 tooltip"
                       SnippetEntry.Group = "group4" } |]
                  |> Store.addSnippets tmp.GetSnippetPath
                  |> Expect.wantOk "should return Ok"
                  |> Expect.equal "should return snippets" ()

                  let expected =
                      """{
  "SearchCaseSensitive": false,
  "Snippets": [
    {
      "Snippet": "echo '4'",
      "Tooltip": "4 tooltip",
      "Group": "group4"
    }
  ]
}"""
                      |> normalizeNewlines

                  tmp.GetSnippetContent()
                  |> Expect.equal "should add the snippet to snippet file" expected

              }

              ]

    [<Tests>]
    let tests_removeSnippets =
        testList
            "removeSnippets"
            [

              test "when snippet file is not found" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, "not-found.json")

                  [ "echo '1'" ]
                  |> Store.removeSnippets (fun () -> tmp.Path, path)
                  |> Expect.wantOk "should return Ok"
                  |> Expect.equal "should return Ok" ()

                  Directory.GetFiles(tmp.Path)
                  |> Expect.isEmpty "should not create the snippet file"
              }

              test "when snippet file is invalid" {
                  use tmp = new TempFile(".snippet-predictor-invalid.json", """{"Snippets":[}""")

                  [ "echo '2'" ]
                  |> Store.removeSnippets tmp.GetSnippetPath
                  |> Expect.wantError "should return Error"
                  |> Expect.equal
                      "should return Error entry"
                      "'An error occurred while parsing .snippet-predictor.json': '}' is an invalid start of a value. Path: $.Snippets[0] | LineNumber: 0 | BytePositionInLine: 13."
              }

              test "when snippet file is valid" {
                  use tmp =
                      new TempFile(
                          ".snippet-predictor-valid.json",
                          """{"Snippet": "echo '3'", "Tooltip": "3 tooltip"}"""
                      )

                  [ "echo '1'"; "echo '3'"; "echo '3'" ]
                  |> Store.removeSnippets tmp.GetSnippetPath
                  |> Expect.wantOk "should return Ok"
                  |> Expect.equal "should return snippets" ()

                  let expected =
                      """{
  "SearchCaseSensitive": false,
  "Snippets": []
}"""
                      |> normalizeNewlines

                  tmp.GetSnippetContent()
                  |> Expect.equal "should remove the snippet from snippet file" expected
              }

              ]

#if DEBUG

module GroupJsonConverter =
    open System.Text.Json

    [<Tests>]
    let test_GroupJsonConverter =
        testList
            "GroupJsonConverter"
            [

              test "when the value is null " {
                  let json = """{"key": null}"""
                  let mutable reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json))

                  reader.Read() |> ignore // {
                  reader.Read() |> ignore // "key"
                  reader.Read() |> ignore // null

                  let result: string | null =
                      GroupJsonConverter().Read(&reader, typeof<string>, JsonSerializerOptions())

                  match result with
                  | null -> failtest "Expected empty string but got null"
                  | value when value.Length = 0 -> ()
                  | _ -> failtest "Expected empty string but got a different value"
              }

              ]

#endif
