module SnippetPredictorTest.Library

open Expecto
open Expecto.Flip

open SnippetPredictor
open SnippetPredictorTest.Utility

open System
open System.IO
open System.Management.Automation

module Mock =
    type CommandRuntime() =
        let mutable output: obj list = List.empty
        let mutable errors: ErrorRecord list = List.empty
        let mutable warnings: string list = List.empty

        let write (obj: obj | null) =
            match obj with
            | null -> failwith "null"
            | obj -> output <- (nullArgCheck "obj" obj) :: output

        member __.Output = output
        member __.Errors = errors
        member __.Warnings = warnings

        interface ICommandRuntime with
            member __.Host = null
            member __.CurrentPSTransaction = null
            member __.WriteDebug(_) = ()
            member __.WriteError(errorRecord: ErrorRecord) = errors <- errorRecord :: errors

            member __.WriteObject(obj: obj) = write obj

            member __.WriteObject(obj: obj, _) = write obj

            member __.WriteProgress _ = ()
            member __.WriteProgress(_, _) = ()
            member __.WriteVerbose(_) = ()
            member __.WriteWarning(warning: string) = warnings <- warning :: warnings

            member __.WriteCommandDetail(_) = ()
            member __.ShouldProcess(_) = true
            member __.ShouldProcess(_, _) = true
            member __.ShouldProcess(_, _, _) = true
            member __.ShouldProcess(_, _, _, _) = true
            member __.ShouldContinue(_, _) = true
            member __.ShouldContinue(_, _, _, _) = true
            member __.TransactionAvailable() = true
            member __.ThrowTerminatingError(errorRecord: ErrorRecord) = errors <- errorRecord :: errors

let assertErrorRecord (expected: ErrorRecord) (e: ErrorRecord) =
    e.Exception.Message = expected.Exception.Message
    && e.CategoryInfo.Category = expected.CategoryInfo.Category
    && e.TargetObject = expected.TargetObject

module AddSnippet =
    type AddSnippetCommandForTest(getSnippetPath) =
        inherit AddSnippetCommand()

        override __.GetSnippetPath() = getSnippetPath ()
        member val Runtime = Mock.CommandRuntime()

        // NOTE: PSCmdlet cannot invoke directly. So, use this method for testing.
        member __.Test() =
            __.CommandRuntime <- __.Runtime
            __.BeginProcessing()
            __.ProcessRecord()
            __.EndProcessing()

    [<Tests>]
    let tests_AddSnippet =

        testList
            "AddSnippet"
            [

              test "when snippet file is valid" {
                  use tmp = new TempFile(".snippet-predictor.json", """{"Snippets": []}""")

                  let cmdlet = AddSnippetCommandForTest(tmp.GetSnippetPath)
                  cmdlet.Snippet <- "Add-Snippet 'echo test'"
                  cmdlet.Tooltip <- "add snippet"
                  cmdlet.Group <- "test"
                  cmdlet.Test() |> ignore

                  let expected =
                      """{
  "Snippets": [
    {
      "Snippet": "Add-Snippet 'echo test'",
      "Tooltip": "add snippet",
      "Group": "test"
    }
  ]
}"""
                      |> normalizeNewlines

                  tmp.GetSnippetContent()
                  |> Expect.equal "should add the snippet to snippet file" expected

              }

              test "when snippet file is invalid" {
                  use tmp = new TempFile(".snippet-predictor.json", """{"Snippets": [}""")

                  let cmdlet = AddSnippetCommandForTest(tmp.GetSnippetPath)
                  cmdlet.Snippet <- "Add-Snippet 'echo test'"
                  cmdlet.Tooltip <- "add snippet"
                  cmdlet.Group <- "test"
                  cmdlet.Test() |> ignore

                  let expected = """{"Snippets": [}""" |> normalizeNewlines

                  tmp.GetSnippetContent()
                  |> Expect.equal "shouldn't add the snippet to snippet file" expected

                  cmdlet.Runtime.Errors |> Expect.isNonEmpty "should have error"

                  let expected =
                      ErrorRecord(
                          Exception(
                              "'An error occurred while parsing .snippet-predictor.json': '}' is an invalid start of a value. Path: $.Snippets[0] | LineNumber: 0 | BytePositionInLine: 14."
                          ),
                          "",
                          ErrorCategory.InvalidData,
                          null
                      )

                  cmdlet.Runtime.Errors
                  |> Expect.all "should have error" (assertErrorRecord expected)
              }

              ]

module GetSnippet =
    type GetSnippetCommandForTest(getSnippetPath) =
        inherit GetSnippetCommand()

        override __.GetSnippetPath() = getSnippetPath ()
        member val Runtime = Mock.CommandRuntime()

        // NOTE: PSCmdlet cannot invoke directly. So, use this method for testing.
        member __.Test() =
            __.CommandRuntime <- __.Runtime
            __.BeginProcessing()
            __.ProcessRecord()
            __.EndProcessing()

    [<Tests>]
    let tests_GetSnippet =
        testList
            "GetSnippet"
            [

              test "when snippet file is valid" {
                  use tmp =
                      new TempFile(
                          ".snippet-predictor.json",
                          """{"Snippets": [{"Snippet": "Get-Snippet", "Tooltip": "get snippet", "Group": "test"}]}"""
                      )

                  let cmdlet = GetSnippetCommandForTest(tmp.GetSnippetPath)
                  cmdlet.Test() |> ignore

                  let expected: SnippetEntry =
                      { Snippet = "Get-Snippet"
                        Tooltip = "get snippet"
                        Group = "test" }

                  cmdlet.Runtime.Output
                  |> Expect.all "should have snippet" (fun output ->
                      let actual = output :?> SnippetEntry
                      actual = expected)
              }

              test "when snippet file is invalid" {
                  use tmp = new TempFile(".snippet-predictor.json", """{"Snippets": [}""")

                  let cmdlet = GetSnippetCommandForTest(tmp.GetSnippetPath)
                  cmdlet.Test() |> ignore

                  let expected = """{"Snippets": [}""" |> normalizeNewlines

                  tmp.GetSnippetContent()
                  |> Expect.equal "shouldn't add the snippet to snippet file" expected

                  cmdlet.Runtime.Errors |> Expect.isNonEmpty "should have error"

                  let expected =
                      ErrorRecord(
                          Exception(
                              "'An error occurred while parsing .snippet-predictor.json': '}' is an invalid start of a value. Path: $.Snippets[0] | LineNumber: 0 | BytePositionInLine: 14."
                          ),
                          "",
                          ErrorCategory.InvalidData,
                          null
                      )

                  cmdlet.Runtime.Errors
                  |> Expect.all "should have error" (assertErrorRecord expected)
              } ]

module RemoveSnippet =
    type RemoveSnippetCommandForTest(getSnippetPath) =
        inherit RemoveSnippetCommand()

        override __.GetSnippetPath() = getSnippetPath ()
        member val Runtime = Mock.CommandRuntime()

        // NOTE: PSCmdlet cannot invoke directly. So, use this method for testing.
        member __.Test() =
            __.CommandRuntime <- __.Runtime
            __.BeginProcessing()
            __.ProcessRecord()
            __.EndProcessing()

    [<Tests>]
    let tests_RemoveSnippet =
        testList
            "RemoveSnippet"
            [

              test "when snippet file is valid" {
                  use tmp =
                      new TempFile(
                          ".snippet-predictor.json",
                          """{"Snippets": [{"Snippet": "Remove-Snippet", "Tooltip": "remove snippet", "Group": "test"}]}"""
                      )

                  let cmdlet = RemoveSnippetCommandForTest(tmp.GetSnippetPath)
                  cmdlet.Snippet <- "Remove-Snippet"
                  cmdlet.Test() |> ignore

                  let expected =
                      """{
  "Snippets": []
}"""
                      |> normalizeNewlines

                  tmp.GetSnippetContent()
                  |> normalizeNewlines
                  |> Expect.equal "should remove the snippet from snippet file" expected
              }

              test "when snippet file is invalid" {
                  use tmp = new TempFile(".snippet-predictor.json", """{"Snippets": [}""")

                  let cmdlet = RemoveSnippetCommandForTest(tmp.GetSnippetPath)
                  cmdlet.Test() |> ignore

                  let expected = """{"Snippets": [}""" |> normalizeNewlines

                  tmp.GetSnippetContent()
                  |> Expect.equal "shouldn't remove the snippet from snippet file" expected

                  cmdlet.Runtime.Errors |> Expect.isNonEmpty "should have error"

                  let expected =
                      ErrorRecord(
                          Exception(
                              "'An error occurred while parsing .snippet-predictor.json': '}' is an invalid start of a value. Path: $.Snippets[0] | LineNumber: 0 | BytePositionInLine: 14."
                          ),
                          "",
                          ErrorCategory.InvalidData,
                          null
                      )

                  cmdlet.Runtime.Errors
                  |> Expect.all "should have error" (assertErrorRecord expected)
              } ]

module SnippetPredictorInitialization =
    open System.Management.Automation.Subsystem

    let createMockModule () =
        use ps =
            PowerShell.Create().AddScript("New-Module -Name MockModule -ScriptBlock { function TestFunc {} }")

        let results = ps.Invoke<PSModuleInfo>()

        if results.Count = 0 then
            failwith "No module created"
        else
            results[0]

    let getSnippetPredictorSubsystem () =
        SubsystemManager.GetAllSubsystemInfo()
        |> Seq.filter (fun x -> x.Kind = SubsystemKind.CommandPredictor)
        |> Seq.collect _.Implementations
        |> Seq.filter (fun x -> x.Name = Snippet.name)
        |> Seq.tryHead

    [<Tests>]
    let tests_Init =
        testList
            "Init"
            [

              test "run" {
                  let subsystem = Init()
                  (subsystem :> IModuleAssemblyInitializer).OnImport()

                  let predictor = getSnippetPredictorSubsystem ()

                  predictor |> Expect.isSome "should have Snippet predictor"
                  let predictor = predictor.Value

                  predictor.Description
                  |> Expect.equal "Description" "A predictor that suggests a snippet based on the input."

                  predictor.Id |> Expect.equal "Id"
                  <| Guid.Parse("f6dbcf05-2f90-4c47-b40e-6a4cec337cc1")

                  (subsystem :> IModuleAssemblyCleanup).OnRemove(createMockModule ())

                  let predictor = getSnippetPredictorSubsystem ()
                  predictor |> Expect.isNone "should remove Snippet predictor"
              }

              ]

module SnippetPredictor =
    open System.Management.Automation.Subsystem.Prediction
    open System.Threading

    let getSnippetPath (path: string) =
        let directory = Path.GetDirectoryName(path)

        (nullArgCheck "directory" directory, path)

    type SnippetPredictorForTest(path) =
        inherit SnippetPredictor("f6dbcf05-2f90-4c47-b40e-6a4cec337cc1", fun () -> getSnippetPath path)

    [<Tests>]
    let tests_SnippetPredictor =
        testList
            "SnippetPredictor"
            [

              test "GetSuggestion" {
                  let predictor =
                      SnippetPredictorForTest("./.snippet-predictor-valid.json") :> ICommandPredictor

                  // NOTE: This is a workaround for the test; the test crashes without a proper wait.
                  Async.Sleep(1000) |> Async.RunSynchronously

                  let client = PredictionClient("test", PredictionClientKind.Terminal)

                  let result =
                      predictor.GetSuggestion(client, PredictionContext.Create(":group"), CancellationToken.None)

                  result.SuggestionEntries
                  |> Expect.isNonEmpty "should provide suggestions for matching input"

                  result.SuggestionEntries
                  |> Expect.all "should provide suggestions for matching input" (fun entry ->
                      entry.SuggestionText = "echo 'example'"
                      && entry.ToolTip = "[group]example  tooltip")

                  predictor.GetSuggestion(client, PredictionContext.Create(":test"), CancellationToken.None)
                  |> _.SuggestionEntries
                  |> Expect.isNull "should not provide suggestions when no match is found"

              }

              test "for coverage" {
                  let predictor =
                      SnippetPredictorForTest("./.snippet-predictor-valid.json") :> ICommandPredictor

                  predictor.FunctionsToDefine
                  |> Expect.isEmpty "should not have functions to define"

                  let client = PredictionClient("test", PredictionClientKind.Terminal)

                  predictor.CanAcceptFeedback(client, PredictorFeedbackKind.SuggestionDisplayed)
                  |> Expect.isFalse "should not accept feedback"

                  predictor.OnSuggestionDisplayed(client, 0u, 0)
                  predictor.OnSuggestionAccepted(client, 0u, "test")
                  predictor.OnCommandLineAccepted(client, [||])
                  predictor.OnCommandLineExecuted(client, "tes", true)
              }

              ]
