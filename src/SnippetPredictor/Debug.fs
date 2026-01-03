namespace SnippetPredictor

open System
open System.IO

[<AutoOpen>]
module Debug =
    open System.Runtime.CompilerServices
    open System.Runtime.InteropServices

    let lockObj = new obj ()

    [<Literal>]
    let logPath = "./debug.log"

    [<AbstractClass; Sealed>]
    type Logger =
        static member LogFile
            (
                res,
                [<Optional; DefaultParameterValue(""); CallerMemberName>] caller: string,
                [<CallerFilePath; Optional; DefaultParameterValue("")>] path: string,
                [<CallerLineNumber; Optional; DefaultParameterValue(0)>] line: int
            ) =

            // NOTE: lock to avoid another process error when dotnet test.
            lock lockObj (fun () ->
                use sw = new StreamWriter(logPath, true)

                res
                |> List.iter (
                    fprintfn
                        sw
                        "[%s] %s at %d %s <%A>"
                        (DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"))
                        path
                        line
                        caller
                ))
