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

type SnippetEntry =
    {
        Snippet: string
        Tooltip: string
        [<JsonConverter(typeof<GroupJsonConverter>)>]
        [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
        Group: string | null
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

            match snippet with
            | null -> invalid "Snippet is required and cannot be empty or whitespace."
            | snippet when String.IsNullOrWhiteSpace snippet ->
                invalid "Snippet is required and cannot be empty or whitespace."
            | snippet ->
                {
                    Snippet = snippet
                    Tooltip = tooltip
                    Group = group
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

type SnippetConfig =
    {
        [<JsonConverter(typeof<SearchCaseSensitiveJsonConverter>)>]
        SearchCaseSensitive: bool
        // NOTE: A property-level converter here must target the array, not one SnippetEntry item.
        // A type-level attribute would also affect serialization.
        Snippets: SnippetEntry array | null
    }
