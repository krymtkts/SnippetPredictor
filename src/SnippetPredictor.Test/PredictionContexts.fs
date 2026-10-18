module SnippetPredictorTest.PredictionContexts

open System
open System.Text.Json
open Expecto
open Expecto.Flip
open SnippetPredictor
open SnippetPredictorTest.Utility

let private configuration property =
    "{\"Snippets\":[{\"Snippet\":\"git status\",\"Tooltip\":\"status\",\"Group\":\"git\""
    + property
    + "}]}"

let private load json =
    use file = new TempFile(Config.snippetFilesName, json)
    Store.loadConfig file.GetSnippetPath

let private expectValid =
    function
    | Config.ConfigState.Valid config -> config
    | _ -> failtest "Expected a valid configuration"

let private firstEntry (config: SnippetConfig) =
    match config.Snippets with
    | null -> failtest "Expected snippets"
    | entries -> entries[0]

let private deserializeContext (json: string) =
    let mutable reader = Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json))
    reader.Read() |> ignore
    PredictionContextsJsonConverter().Read(&reader, typeof<PredictionContexts>, JsonSerializerOptions())

[<Tests>]
let tests =
    let both = PredictionContexts.Command ||| PredictionContexts.Pipeline

    testList
        "prediction context configuration"
        [
            for name, property, expected, serialized in
                [|
                    "omitted", "", PredictionContexts.Command, """["Command"]"""
                    "null", ""","PredictionContexts":null""", PredictionContexts.Command, """["Command"]"""
                    "command", ""","PredictionContexts":["Command"]""", PredictionContexts.Command, """["Command"]"""
                    "pipeline",
                    ""","PredictionContexts":["Pipeline"]""",
                    PredictionContexts.Pipeline,
                    """["Pipeline"]"""
                    "both", ""","PredictionContexts":["Command","Pipeline"]""", both, """["Command","Pipeline"]"""
                    "reverse order",
                    ""","PredictionContexts":["Pipeline","Command"]""",
                    both,
                    """["Command","Pipeline"]"""
                    "case insensitive",
                    ""","predictioncontexts":["pIpElInE","COMMAND"]""",
                    both,
                    """["Command","Pipeline"]"""
                    "identifier only",
                    ",\"PredictionContexts\":\"IdentifierOnly\"",
                    PredictionContexts.IdentifierOnly,
                    "\"IdentifierOnly\""
                    "identifier casing",
                    ",\"PREDICTIONCONTEXTS\":\"identifieronly\"",
                    PredictionContexts.IdentifierOnly,
                    "\"IdentifierOnly\""
                |] do
                testCase $"reads and normalizes {name}" (fun () ->
                    let entry = configuration property |> load |> expectValid |> firstEntry

                    entry.PredictionContexts
                    |> Expect.equal "should normalize allowed contexts" expected

                    entry.Snippet |> Expect.equal "should retain snippet text" "git status"
                    entry.Tooltip |> Expect.equal "should retain tooltip" "status"
                    entry.Group |> Expect.equal "should retain group" "git"

                    let json = JsonSerializer.Serialize(entry)
                    use document = JsonDocument.Parse(json)

                    document.RootElement.GetProperty("PredictionContexts").GetRawText()
                    |> Expect.equal "should serialize an explicit canonical value" serialized

                    deserializeContext serialized
                    |> Expect.equal "should deserialize through the property converter" expected

                    """{"Snippets":[""" + json + "]}"
                    |> load
                    |> expectValid
                    |> firstEntry
                    |> Expect.equal "should round trip the complete entry" entry)

            for value in
                [|
                    "[]"
                    "{}"
                    "true"
                    "false"
                    "0"
                    "1"
                    "3"
                    "\"\""
                    "\" \""
                    "\"Command\""
                    "\"Pipeline\""
                    "\"Unsupported\""
                    "\" IdentifierOnly\""
                    "\"IdentifierOnly \""
                    """["IdentifierOnly"]"""
                    """["Command","IdentifierOnly"]"""
                    """["Command",null]"""
                    """["Command",0]"""
                    """["Command",true]"""
                    """["Command",{}]"""
                    """["Command",[]]"""
                    """["Command",""]"""
                    """["Command"," "]"""
                    """["Command"," Pipeline"]"""
                    """["Command","Unsupported"]"""
                    """["Command","Command"]"""
                    """["Pipeline","Pipeline"]"""
                    """["Command","command"]"""
                    """["Pipeline","PIPELINE"]"""
                    """["Command","Pipeline","Command"]"""
                    """["Pipeline","Command","Pipeline"]"""
                    """["Command","\u0043ommand"]"""
                |] do
                testCase $"rejects {value}" (fun () ->
                    match configuration (",\"PredictionContexts\":" + value) |> load with
                    | Config.ConfigState.Invalid entry ->
                        entry.PredictionContexts
                        |> Expect.equal "should initialize diagnostic entries" PredictionContexts.Command

                        entry.Tooltip.Contains("PredictionContexts")
                        |> Expect.isTrue "should identify the invalid setting"

                        entry.Tooltip.Contains("$.Snippets[0]")
                        |> Expect.isTrue "should include the entry path"
                    | _ -> failtest "Expected an invalid configuration")

            for name, json in
                [|
                    "first property on one line",
                    """{"Snippets":[{"PredictionContexts":["Pipeline","Command"],"Snippet":"git status","Tooltip":"status","Group":"git"}]}"""
                    "middle property on one line",
                    """{"Snippets":[{"Snippet":"git status","PredictionContexts":["Pipeline","Command"],"Tooltip":"status","Group":"git"}]}"""
                    "last property on one line",
                    """{"Snippets":[{"Snippet":"git status","Tooltip":"status","Group":"git","PredictionContexts":["Pipeline","Command"]}]}"""
                    "first property across multiple lines",
                    """{
                        "Snippets": [
                            {
                                "PredictionContexts": [
                                    "Pipeline",
                                    "Command"
                                ],
                                "Snippet": "git status",
                                "Tooltip": "status",
                                "Group": "git"
                            }
                        ]
                    }"""
                |] do
                testCase $"reads {name}" (fun () ->
                    let expected =
                        { Store.makeSnippetEntry [| PredictionContexts.Command |] "git status" "status" "git" with
                            PredictionContexts = both
                        }

                    json
                    |> load
                    |> expectValid
                    |> firstEntry
                    |> Expect.equal "should not depend on property order or line breaks" expected)

            test "preserves all contexts when saving configuration" {
                let json =
                    """{"SearchCaseSensitive":true,"PredictOnlyWithIdentifier":true,"Snippets":[
                        {"Snippet":"git status","PredictionContexts":null},
                        {"Snippet":"Select-Object Name","PredictionContexts":["Pipeline"]},
                        {"Snippet":"git log","PredictionContexts":["Pipeline","Command"]},
                        {"Snippet":"git push --force-with-lease","PredictionContexts":"IdentifierOnly"}
                    ]}"""

                use file = new TempFile(Config.snippetFilesName, json)
                let config = Store.loadConfig file.GetSnippetPath |> expectValid

                config
                |> Config.storeConfig file.GetSnippetPath
                |> Expect.wantOk "should save configuration"
                |> ignore

                Store.loadConfig file.GetSnippetPath
                |> expectValid
                |> Expect.equal "should preserve settings and context permissions" config

                use document = JsonDocument.Parse(file.GetSnippetContent())
                let entries = document.RootElement.GetProperty("Snippets")

                entries[0].GetProperty("PredictionContexts").ValueKind
                |> Expect.equal "should write default command as an array" JsonValueKind.Array

                entries[3].GetProperty("PredictionContexts").GetString()
                |> Expect.equal "should keep identifier-only mode explicit" "IdentifierOnly"
            }

            test "identifies duplicate context values in diagnostics" {
                for value in [| """["Command","command"]"""; """["Pipeline","\u0050ipeline"]""" |] do
                    match configuration (",\"PredictionContexts\":" + value) |> load with
                    | Config.ConfigState.Invalid entry ->
                        entry.Tooltip.Contains("Duplicate value in PredictionContexts:")
                        |> Expect.isTrue "should identify the repeated context setting"

                        entry.Tooltip.Contains("$.Snippets[0]")
                        |> Expect.isTrue "should preserve the entry path"
                    | _ -> failtest "Expected an invalid configuration"
            }

            test "preserves repeated entries and independent field values" {
                let first = Store.makeSnippetEntry [| both |] "git status" "status" "git"

                let entries =
                    [|
                        first
                        first
                        { first with Tooltip = "other" }
                        { first with Group = "other" }
                        { first with
                            PredictionContexts = PredictionContexts.Command
                        }
                        { first with Snippet = "git log" }
                        { first with Tooltip = first.Snippet }
                    |]

                use file = new TempFile(Config.snippetFilesName, """{"Snippets":[]}""")

                entries
                |> Store.addSnippets file.GetSnippetPath
                |> Expect.wantOk "should retain independent entries"
                |> ignore

                [| first |]
                |> Store.addSnippets file.GetSnippetPath
                |> Expect.wantOk "should allow repeated registration"
                |> ignore

                Store.loadSnippets file.GetSnippetPath
                |> Expect.wantOk "should load every entry"
                |> Expect.equal "should preserve values and configuration order" (Array.append entries [| first |])
            }

            test "creates entries with an explicit command context" {
                Store.makeSnippetEntry [| PredictionContexts.Command |] "git status" "status" null
                |> _.PredictionContexts
                |> Expect.equal "should use the requested context" PredictionContexts.Command
            }

            test "defaults null through the property converter to command" {
                deserializeContext "null"
                |> Expect.equal "should distinguish null from identifier-only mode" PredictionContexts.Command
            }

            for name, contexts, expected in
                [|
                    "command", [| PredictionContexts.Command |], PredictionContexts.Command
                    "pipeline", [| PredictionContexts.Pipeline |], PredictionContexts.Pipeline
                    "both", [| PredictionContexts.Command; PredictionContexts.Pipeline |], both
                    "combined flags", [| both |], both
                    "identifier only", [| PredictionContexts.IdentifierOnly |], PredictionContexts.IdentifierOnly
                |] do
                testCase $"creates entries for {name}" (fun () ->
                    let entry = Store.makeSnippetEntry contexts "git status" "status" "git"

                    entry.PredictionContexts
                    |> Expect.equal "should combine context permissions" expected

                    entry.Snippet |> Expect.equal "should retain snippet" "git status"
                    entry.Tooltip |> Expect.equal "should retain tooltip" "status"
                    entry.Group |> Expect.equal "should retain group" "git")

            for name, contexts in
                [|
                    "empty", [||]
                    "unknown", [| enum<PredictionContexts> 4 |]
                    "negative", [| enum<PredictionContexts> -1 |]
                    "unknown combined", [| PredictionContexts.Command; enum<PredictionContexts> 4 |]
                    "identifier with command", [| PredictionContexts.IdentifierOnly; PredictionContexts.Command |]
                    "pipeline with identifier", [| PredictionContexts.Pipeline; PredictionContexts.IdentifierOnly |]
                    "identifier with both", [| PredictionContexts.IdentifierOnly; both |]
                    "duplicate command", [| PredictionContexts.Command; PredictionContexts.Command |]
                    "duplicate pipeline", [| PredictionContexts.Pipeline; PredictionContexts.Pipeline |]
                    "separated duplicate",
                    [|
                        PredictionContexts.Command
                        PredictionContexts.Pipeline
                        PredictionContexts.Command
                    |]
                    "repeated identifier only",
                    [| PredictionContexts.IdentifierOnly; PredictionContexts.IdentifierOnly |]
                    "overlapping combined flags", [| both; PredictionContexts.Command |]
                    "reverse overlapping flags", [| PredictionContexts.Pipeline; both |]
                    "repeated combined flags", [| both; both |]
                |] do
                testCase $"rejects entry creation for {name}" (fun () ->
                    let rejected =
                        try
                            Store.makeSnippetEntry contexts "git status" "status" null |> ignore
                            false
                        with :? ArgumentException ->
                            true

                    rejected |> Expect.isTrue "should reject invalid context permissions")

            test "preserves contexts when adding and removing snippets" {
                let entries =
                    [|
                        Store.makeSnippetEntry [| PredictionContexts.Command |] "git status" "status" "git"
                        Store.makeSnippetEntry [| PredictionContexts.Pipeline |] "Select-Object Name" "select" null
                        Store.makeSnippetEntry [| both |] "git log" "log" "git"
                        Store.makeSnippetEntry [| PredictionContexts.IdentifierOnly |] "git push" "push" "git"
                    |]

                use file = new TempFile(Config.snippetFilesName, """{"Snippets":[]}""")

                entries
                |> Store.addSnippets file.GetSnippetPath
                |> Expect.wantOk "should add every context selection"
                |> ignore

                Store.loadSnippets file.GetSnippetPath
                |> Expect.wantOk "should load snippets"
                |> Expect.equal "should retain all entry properties" entries

                let extra =
                    Store.makeSnippetEntry [| PredictionContexts.Command |] "git diff" "diff" null

                [| extra |]
                |> Store.addSnippets file.GetSnippetPath
                |> Expect.wantOk "should append a snippet"
                |> ignore

                [| extra.Snippet |]
                |> Store.removeSnippets file.GetSnippetPath
                |> Expect.wantOk "should remove the extra snippet"
                |> ignore

                Store.loadSnippets file.GetSnippetPath
                |> Expect.wantOk "should reload snippets"
                |> Expect.equal "should preserve contexts through later edits" entries
            }

            test "rejects undefined flags when serializing" {
                for value in [| enum<PredictionContexts> 4; enum<PredictionContexts> -1 |] do
                    let entry =
                        { Store.makeSnippetEntry [| PredictionContexts.Command |] "git status" "status" null with
                            PredictionContexts = value
                        }

                    let rejected =
                        try
                            JsonSerializer.Serialize(entry) |> ignore
                            false
                        with :? JsonException ->
                            true

                    rejected |> Expect.isTrue "should not serialize unknown permissions"
            }
        ]
