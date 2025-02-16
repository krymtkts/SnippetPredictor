module Snippet

open System
open System.IO
open System.Collections
open System.Management.Automation.Subsystem.Prediction
open System.Text.Json
open System.Threading

type SnippetEntry = { snippet: string; tooltip: string }
type Snippets = { snippets: SnippetEntry[] }

[<Literal>]
let snippetFilesName = ".snippet-predictor.json"

[<Literal>]
let snippetSymbol = ":snp"

let snippets = Concurrent.ConcurrentQueue<SnippetEntry>()

let parseSnippets (json: string) =
    try
        json
        |> JsonSerializer.Deserialize<Snippets>
        |> function
            | null -> Array.empty
            | _ as snippets -> snippets.snippets
    with _ ->
        Array.empty

let load () =
    let snippetPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), snippetFilesName)

    if File.Exists(snippetPath) then
        let cancellationToken = new CancellationToken()

        task {
            let! json = snippetPath |> File.ReadAllTextAsync

            json |> parseSnippets |> Array.iter snippets.Enqueue
        }
        |> _.WaitAsync(cancellationToken)
        |> ignore

let getFilter (input: string) =
    // NOTE: Remove the snippet symbol from the input.
    // NOTE: Snippet symbol is used to exclude other predictors from suggestions.
    input.Replace(snippetSymbol, "")

let getSnippets (filter: string) : SnippetEntry seq =
    snippets |> Seq.filter _.snippet.Contains(filter)

let snippetToTuple (s: SnippetEntry) = s.snippet, s.tooltip

let getPredictiveSuggestions (input: string) : Generic.List<PredictiveSuggestion> =
    if String.IsNullOrWhiteSpace(input) then
        Seq.empty
    else
        input
        |> (getFilter >> getSnippets)
        |> Seq.map (snippetToTuple >> PredictiveSuggestion)
    |> Linq.Enumerable.ToList
