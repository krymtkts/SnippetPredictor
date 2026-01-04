namespace SnippetPredictor

module Suggestion =

#if DEBUG
    module Disposal =
        type Flag =
            new: unit -> Flag
            member IsDisposed: bool with get
            member TryMarkDisposed: unit -> bool
            member IfNotDisposed: f: (unit -> unit) -> unit
            member IfDisposed: f: (unit -> unit) -> unit
#endif

    type Cache =
        interface System.IDisposable

        new: unit -> Cache

        abstract CreateWatcher: directory: string * filter: string -> System.IO.FileSystemWatcher

        abstract OnRefresh: path: string -> unit

        member getPredictiveSuggestions:
            input: string ->
                System.Collections.Generic.List<System.Management.Automation.Subsystem.Prediction.PredictiveSuggestion>

        member load: getSnippetPath: (unit -> string * string) -> unit

    val getSnippetPath: (unit -> string * string)
