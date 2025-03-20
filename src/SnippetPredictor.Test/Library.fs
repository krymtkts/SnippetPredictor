module SnippetPredictorTest.Library

open Expecto
open Expecto.Flip

open SnippetPredictor
open SnippetPredictorTest.Utility

open System.IO


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

              ]
