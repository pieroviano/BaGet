using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Html;

namespace BaGet.Identity.Application.Screens;

/// <summary>The JSON of <c>/api/ui</c>: camelCase, enums as their names, nulls left out (the same shape as the administration API).</summary>
public static class UiApiJson
{
    /// <summary>The serializer options of every UI API response.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Explicit: an output formatter makes the options read-only, which needs a resolver.
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new HtmlStringConverter());

        return options;
    }

    /// <summary>
    /// An <see cref="HtmlString"/> (rendered, already safe HTML such as a readme) is written as the plain string it holds, not as an object, so a client
    /// receives the HTML as text.
    /// </summary>
    private sealed class HtmlStringConverter : JsonConverter<HtmlString>
    {
        public override HtmlString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? null : new HtmlString(reader.GetString());

        public override void Write(Utf8JsonWriter writer, HtmlString value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }
}
