namespace SnippetPredictor

module PredictionInput =
    open System
    open System.Management.Automation.Language
    open System.Management.Automation.Subsystem.Prediction

    let private isWhitespace (input: string) start finish =
        let rec loop position =
            if position >= finish then
                true
            elif Char.IsWhiteSpace(input[position]) then
                loop (position + 1)
            else
                false

        loop start

    let private isTerminalCommand (context: PredictionContext) (command: CommandAst) =
        let isEndBlock (block: NamedBlockAst) =
            match block.Parent with
            | :? ScriptBlockAst as script ->
                (script :> Ast) = context.InputAst
                && script.EndBlock = block
                && isNull script.BeginBlock
                && isNull script.ProcessBlock
            | _ -> false

        match command.Parent with
        | :? PipelineAst as pipeline ->
            match pipeline.Parent with
            | :? NamedBlockAst as block ->
                isEndBlock block
                && block.Statements.Count > 0
                && block.Statements[block.Statements.Count - 1] = pipeline
                && pipeline.PipelineElements[pipeline.PipelineElements.Count - 1] = command
            | _ -> false
        | _ -> false

    let tryCommandRange (context: PredictionContext) =
        let relatedAsts = context.RelatedAsts

        let rec loop index candidate =
            if index >= relatedAsts.Count then
                candidate
            else
                let next =
                    match relatedAsts[index] with
                    | :? CommandAst as command ->
                        match candidate with
                        | ValueSome(previous: CommandAst) when previous.Extent.StartOffset >= command.Extent.StartOffset ->
                            candidate
                        | _ -> ValueSome command
                    | _ -> candidate

                loop (index + 1) next

        let candidate = loop 0 ValueNone
        let input = context.InputAst.Extent.Text

        match candidate with
        | ValueSome command when
            isTerminalCommand context command
            && command.Redirections.Count = 0
            && command.InvocationOperator = TokenKind.Unknown
            && not (
                command.CommandElements
                |> Seq.exists (function
                    | :? ScriptBlockExpressionAst
                    | :? SubExpressionAst -> true
                    | _ -> false)
            )
            && (match context.TokenAtCursor with
                | null -> true
                | token -> token.TokenFlags &&& TokenFlags.TokenInError = TokenFlags.None)
            && not (isWhitespace input 0 command.Extent.StartOffset)
            && isWhitespace input command.Extent.EndOffset input.Length
            ->
            match command.CommandElements[0] with
            | :? StringConstantExpressionAst as name when name.StringConstantType = StringConstantType.BareWord ->
                if name.Value.StartsWith(":", StringComparison.Ordinal) then
                    ValueNone
                else
                    ValueSome(struct (command.Extent.StartOffset, input.Length - command.Extent.StartOffset))
            | _ -> ValueNone
        | _ -> ValueNone
