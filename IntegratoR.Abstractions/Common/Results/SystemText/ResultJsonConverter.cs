using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;

namespace IntegratoR.Abstractions.Common.Results.SystemText;

/// <summary>
/// System.Text.Json factory that produces converters for any closed <see cref="Result{T}"/> type.
/// Register this once on a <see cref="JsonSerializerOptions"/> instance to enable round-tripping
/// of <see cref="Result{T}"/> through code paths that use System.Text.Json — notably
/// the Durable Functions isolated worker (<c>JsonDataConverter</c>) and the distributed cache.
/// </summary>
/// <remarks>
/// The non-generic <see cref="Result"/> type is intentionally not handled here because no current
/// System.Text.Json code path in the codebase serialises it. Add a converter for it if that changes.
/// </remarks>
public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType
            && !typeToConvert.IsGenericTypeDefinition
            && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type valueType = typeToConvert.GetGenericArguments()[0];
        Type converterType = typeof(ResultJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// System.Text.Json converter for a closed <see cref="Result{T}"/>. Mirrors the JSON shape
/// produced by the Newtonsoft.Json converters in <c>ResultJsonConverter</c> for cross-serialiser
/// compatibility:
/// <code>
/// { "isSuccess": true,  "value": { ... } }
/// { "isSuccess": false, "errors": [ { "code": "...", "message": "...", "type": "..." } ] }
/// </code>
/// Property names and the IError ↔ primitives mapping are owned by <see cref="ResultJsonShape"/>
/// so both converters stay in lockstep.
/// </summary>
/// <typeparam name="T">The value type wrapped by <see cref="Result{T}"/>.</typeparam>
public sealed class ResultJsonConverter<T> : JsonConverter<Result<T>>
{
    public override Result<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;

        bool isSuccess = root.TryGetProperty(ResultJsonShape.IsSuccess, out JsonElement isSuccessElement)
            && isSuccessElement.GetBoolean();

        if (isSuccess)
        {
            T? value = default;
            if (root.TryGetProperty(ResultJsonShape.Value, out JsonElement valueElement)
                && valueElement.ValueKind != JsonValueKind.Null)
            {
                value = valueElement.Deserialize<T>(options);
            }

            return Result.Ok(value!);
        }

        return Result.Fail<T>(StjResultErrorSerializer.Read(root));
    }

    public override void Write(Utf8JsonWriter writer, Result<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean(ResultJsonShape.IsSuccess, value.IsSuccess);

        if (value.IsSuccess)
        {
            writer.WritePropertyName(ResultJsonShape.Value);
            JsonSerializer.Serialize(writer, value.Value, options);
        }
        else
        {
            writer.WritePropertyName(ResultJsonShape.Errors);
            StjResultErrorSerializer.Write(writer, value.Errors);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// System.Text.Json plumbing for the errors array. Lives outside the generic
/// <see cref="ResultJsonConverter{T}"/> so it isn't duplicated per closed <c>T</c>, and delegates
/// to <see cref="ResultJsonShape"/> for the actual IError ↔ primitives mapping.
/// </summary>
internal static class StjResultErrorSerializer
{
    public static void Write(Utf8JsonWriter writer, IReadOnlyList<IError> errors)
    {
        writer.WriteStartArray();
        foreach (IError error in errors)
        {
            (string code, string message, ErrorType type) = ResultJsonShape.Project(error);

            writer.WriteStartObject();
            writer.WriteString(ResultJsonShape.Code, code);
            writer.WriteString(ResultJsonShape.Message, message);
            writer.WriteString(ResultJsonShape.Type, type.ToString());
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    public static List<IError> Read(JsonElement root)
    {
        if (!root.TryGetProperty(ResultJsonShape.Errors, out JsonElement errorsElement)
            || errorsElement.ValueKind != JsonValueKind.Array)
        {
            return new List<IError>();
        }

        List<IError> errors = new(errorsElement.GetArrayLength());

        foreach (JsonElement errorElement in errorsElement.EnumerateArray())
        {
            string? code = errorElement.TryGetProperty(ResultJsonShape.Code, out JsonElement codeElement)
                ? codeElement.GetString()
                : null;

            string? message = errorElement.TryGetProperty(ResultJsonShape.Message, out JsonElement messageElement)
                ? messageElement.GetString()
                : null;

            string? type = errorElement.TryGetProperty(ResultJsonShape.Type, out JsonElement typeElement)
                ? typeElement.GetString()
                : null;

            errors.Add(ResultJsonShape.Hydrate(code, message, type));
        }

        return errors;
    }
}
