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
        let mutable output: string list = List.empty
        let mutable errors: ErrorRecord list = List.empty
        let mutable warnings: string list = List.empty

        let write (obj: obj | null) =
            match obj with
            | null -> failwith "null"
            | obj -> output <- (nullArgCheck "obj" (obj.ToString())) :: output

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

module AddSnippet =
    type AddSnippetCommandForTest(path: string) =
        inherit AddSnippetCommand()

        let getSnippetPath () =
            let directory = Path.GetDirectoryName(path)

            (nullArgCheck "directory" directory, path)

        override __.GetSnippetPath() = getSnippetPath ()

        // NOTE: PSCmdlet cannot invoke directly. So, use this method for testing.
        member __.Test() =
            __.BeginProcessing()
            __.ProcessRecord()
            __.EndProcessing()

    [<Tests>]
    let tests_AddSnippet =

        testList
            "AddSnippet"
            [

              test "when snippet file is valid" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, ".snippet-predictor.json")
                  File.AppendAllText(path, """{"Snippets": []}""")

                  let cmdlet = AddSnippetCommandForTest(path)
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

                  File.ReadAllText(path)
                  |> normalizeNewlines
                  |> Expect.equal "should add the snippet to snippet file" expected

              }

              test "when snippet file is invalid" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, ".snippet-predictor.json")
                  File.AppendAllText(path, """{"Snippets": [}""")

                  let runtime = Mock.CommandRuntime()
                  let cmdlet = AddSnippetCommandForTest(path)
                  cmdlet.CommandRuntime <- runtime
                  cmdlet.Snippet <- "Add-Snippet 'echo test'"
                  cmdlet.Tooltip <- "add snippet"
                  cmdlet.Group <- "test"
                  cmdlet.Test() |> ignore

                  let expected = """{"Snippets": [}""" |> normalizeNewlines

                  File.ReadAllText(path)
                  |> normalizeNewlines
                  |> Expect.equal "shouldn't add the snippet to snippet file" expected

                  runtime.Errors |> Expect.isNonEmpty "should have error"

                  let expected =
                      ErrorRecord(
                          Exception(
                              "'An error occurred while parsing .snippet-predictor.json': '}' is an invalid start of a value. Path: $.Snippets[0] | LineNumber: 0 | BytePositionInLine: 14."
                          ),
                          "",
                          ErrorCategory.InvalidData,
                          null
                      )

                  runtime.Errors
                  |> Expect.all "should have error" (fun e ->
                      e.Exception.Message = expected.Exception.Message
                      && e.CategoryInfo.Category = expected.CategoryInfo.Category
                      && e.TargetObject = expected.TargetObject)
              }

              ]
