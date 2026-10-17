namespace SnippetPredictor

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions

module Noun =
    [<Literal>]
    let snippet = "Snippet"

module Nullable =
    let dispose (d: 'a | null when 'a :> IDisposable) =
        d
        |> function
            | null -> ()
            | p -> (p :> IDisposable).Dispose()

type internal SnippetConfigValidationException(message: string) =
    inherit JsonException(message)

    member _.Detail = message

// NOTE: A static let generates unreachable code, so this module is used instead for coverage.
module Group =
    [<Literal>]
    let pattern = "^[A-Za-z0-9]+$"

    let regex = Regex(pattern)

    let validate (value: string | null) =
        match value with
        | null -> ""
        | value when regex.IsMatch(value) -> value
        | value ->
            SnippetConfigValidationException(sprintf "Invalid characters in group: %s" value)
            |> raise

type GroupJsonConverter() =
    inherit JsonConverter<string>()

    override _.Read(reader: byref<Utf8JsonReader>, _typeToConvert: Type, options: JsonSerializerOptions) =
        reader.GetString() |> Group.validate

    override _.Write(writer: Utf8JsonWriter, value: string, options: JsonSerializerOptions) =
        value |> writer.WriteStringValue

[<Flags>]
type PredictionContexts =
    | IdentifierOnly = 0
    | Command = 1
    | Pipeline = 2

module internal PredictionContextsJson =
    let invalid () =
        SnippetConfigValidationException(
            "PredictionContexts must be null, \"IdentifierOnly\", or a non-empty array containing only \"Command\" and \"Pipeline\"."
        )
        |> raise

    let read (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Null -> PredictionContexts.Command
        | JsonValueKind.String when
            String.Equals(element.GetString(), "IdentifierOnly", StringComparison.OrdinalIgnoreCase)
            ->
            PredictionContexts.IdentifierOnly
        | JsonValueKind.Array when element.GetArrayLength() > 0 ->
            let mutable contexts = PredictionContexts.IdentifierOnly

            for item in element.EnumerateArray() do
                let context =
                    match item.ValueKind with
                    | JsonValueKind.String ->
                        let name = item.GetString()

                        if String.Equals(name, "Command", StringComparison.OrdinalIgnoreCase) then
                            PredictionContexts.Command
                        elif String.Equals(name, "Pipeline", StringComparison.OrdinalIgnoreCase) then
                            PredictionContexts.Pipeline
                        else
                            invalid ()
                    | _ -> invalid ()

                contexts <- contexts ||| context

            contexts
        | _ -> invalid ()

type PredictionContextsJsonConverter() =
    inherit JsonConverter<PredictionContexts>()

    override _.HandleNull = true

    override _.Read(reader: byref<Utf8JsonReader>, _typeToConvert: Type, _options: JsonSerializerOptions) =
        use document = JsonDocument.ParseValue(&reader)
        PredictionContextsJson.read document.RootElement

    override _.Write(writer: Utf8JsonWriter, value: PredictionContexts, _options: JsonSerializerOptions) =
        if value = PredictionContexts.IdentifierOnly then
            writer.WriteStringValue("IdentifierOnly")
        elif value &&& (PredictionContexts.Command ||| PredictionContexts.Pipeline) = value then
            writer.WriteStartArray()

            if value &&& PredictionContexts.Command <> PredictionContexts.IdentifierOnly then
                writer.WriteStringValue("Command")

            if value &&& PredictionContexts.Pipeline <> PredictionContexts.IdentifierOnly then
                writer.WriteStringValue("Pipeline")

            writer.WriteEndArray()
        else
            PredictionContextsJson.invalid ()

type SnippetEntry =
    {
        Snippet: string
        Tooltip: string
        [<JsonConverter(typeof<GroupJsonConverter>)>]
        [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
        Group: string | null
        [<JsonConverter(typeof<PredictionContextsJsonConverter>)>]
        PredictionContexts: PredictionContexts
    }

type SnippetEntryJsonConverter() =
    inherit JsonConverter<SnippetEntry>()

    let invalid message =
        raise (SnippetConfigValidationException(message))

    let readString propertyName (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Null -> null
        | JsonValueKind.String -> element.GetString()
        | _ -> invalid $"The {propertyName} property must be a string or null."

    override _.HandleNull = true

    override _.Read(reader: byref<Utf8JsonReader>, _typeToConvert: Type, _options: JsonSerializerOptions) =
        match reader.TokenType with
        | JsonTokenType.Null -> invalid "Snippet entry cannot be null."
        | JsonTokenType.StartObject ->
            use document = JsonDocument.ParseValue(&reader)
            let mutable snippet: string | null = null
            let mutable tooltip = ""
            let mutable group: string | null = null
            let mutable predictionContexts = PredictionContexts.Command

            for property in document.RootElement.EnumerateObject() do
                if property.Name.Equals("Snippet", StringComparison.OrdinalIgnoreCase) then
                    snippet <- readString "Snippet" property.Value
                elif property.Name.Equals("Tooltip", StringComparison.OrdinalIgnoreCase) then
                    match readString "Tooltip" property.Value with
                    | null -> tooltip <- ""
                    | value -> tooltip <- value
                elif property.Name.Equals("Group", StringComparison.OrdinalIgnoreCase) then
                    match readString "Group" property.Value with
                    | null -> group <- null
                    | value -> group <- Group.validate value
                elif property.Name.Equals("PredictionContexts", StringComparison.OrdinalIgnoreCase) then
                    predictionContexts <- PredictionContextsJson.read property.Value

            match snippet with
            | null -> invalid "Snippet is required and cannot be empty or whitespace."
            | snippet when String.IsNullOrWhiteSpace snippet ->
                invalid "Snippet is required and cannot be empty or whitespace."
            | snippet ->
                {
                    Snippet = snippet
                    Tooltip = tooltip
                    Group = group
                    PredictionContexts = predictionContexts
                }
        | _ -> invalid "Snippet entry must be a JSON object."

    override _.Write(_writer: Utf8JsonWriter, _value: SnippetEntry, _options: JsonSerializerOptions) =
        NotSupportedException("SnippetEntryJsonConverter is only used for deserialization.")
        |> raise

type ErrorEntry = SnippetEntry

type SearchCaseSensitiveJsonConverter() =
    inherit JsonConverter<bool>()

    override _.Read(reader: byref<Utf8JsonReader>, _typeToConvert: Type, options: JsonSerializerOptions) =
        if reader.TokenType = JsonTokenType.Null then
            false
        else
            reader.GetBoolean()

    override _.Write(writer: Utf8JsonWriter, value: bool, options: JsonSerializerOptions) =
        value |> writer.WriteBooleanValue

type PredictOnlyWithIdentifierJsonConverter() =
    inherit JsonConverter<bool>()

    override _.Read(reader: byref<Utf8JsonReader>, _typeToConvert: Type, _options: JsonSerializerOptions) =
        if reader.TokenType = JsonTokenType.Null then
            false
        else
            reader.GetBoolean()

    override _.Write(writer: Utf8JsonWriter, value: bool, _options: JsonSerializerOptions) =
        value |> writer.WriteBooleanValue

type SnippetConfig =
    {
        [<JsonConverter(typeof<SearchCaseSensitiveJsonConverter>)>]
        SearchCaseSensitive: bool
        [<JsonConverter(typeof<PredictOnlyWithIdentifierJsonConverter>)>]
        PredictOnlyWithIdentifier: bool
        // NOTE: A property-level converter here must target the array, not one SnippetEntry item.
        // A type-level attribute would also affect serialization.
        Snippets: SnippetEntry array | null
    }
