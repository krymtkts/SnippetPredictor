namespace SnippetPredictor

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions

module Nullable =
    let dispose (d: 'a | null when 'a :> IDisposable) =
        d
        |> function
            | null -> ()
            | p -> (p :> IDisposable).Dispose()

// NOTE: A static let generates unreachable code, so this module is used instead for coverage.
module Group =
    [<Literal>]
    let pattern = "^[A-Za-z0-9]+$"

    let regex = Regex(pattern)

type GroupJsonConverter() =
    inherit JsonConverter<string>()

    override _.Read(reader: byref<Utf8JsonReader>, _typeToConvert: Type, options: JsonSerializerOptions) =
        reader.GetString()
        |> function
            | null -> "" // NOTE: unreachable when JsonIgnoreCondition.WhenWritingNull is used.
            | value when Group.regex.IsMatch(value) -> value
            | value -> JsonException(sprintf "Invalid characters in group: %s" value) |> raise

    override _.Write(writer: Utf8JsonWriter, value: string, options: JsonSerializerOptions) =
        value |> writer.WriteStringValue

type SnippetEntry =
    { Snippet: string
      Tooltip: string
      [<JsonConverter(typeof<GroupJsonConverter>)>]
      [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
      Group: string | null }

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
    { [<JsonConverter(typeof<SearchCaseSensitiveJsonConverter>)>]
      SearchCaseSensitive: bool
      Snippets: SnippetEntry array | null }
