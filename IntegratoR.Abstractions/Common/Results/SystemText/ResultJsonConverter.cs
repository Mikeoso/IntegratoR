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
/// System.Text.Json converter for a closed <see cref="Result{T}"/>. Mirrors the JSON shape produced
/// by the Newtonsoft.Json converters in <see cref="ResultJsonConverter"/> for cross-serializer compatibility:
/// <code>
/// { "isSuccess": true,  "value": { ... } }
/// { "isSuccess": false, "errors": [ { "code": "...", "message": "...", "type": "..." } ] }
/// </code>
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

        bool isSuccess = root.TryGetProperty("isSuccess", out JsonElement isSuccessElement)
            && isSuccessElement.GetBoolean();

        if (isSuccess)
        {
            T? value = default;
            if (root.TryGetProperty("value", out JsonElement valueElement)
                && valueElement.ValueKind != JsonValueKind.Null)
            {
                value = valueElement.Deserialize<T>(options);
            }

            return Result.Ok(value!);
        }

        List<IError> errors = ReadErrors(root);
        return Result.Fail<T>(errors);
    }

    public override void Write(Utf8JsonWriter writer, Result<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("isSuccess", value.IsSuccess);

        if (value.IsSuccess)
        {
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, value.Value, options);
        }

        writer.WritePropertyName("errors");
        WriteErrors(writer, value.Errors);

        writer.WriteEndObject();
    }

    internal static void WriteErrors(Utf8JsonWriter writer, IReadOnlyList<IError> errors)
    {
        writer.WriteStartArray();
        foreach (IError error in errors)
        {
            writer.WriteStartObject();
            if (error is IntegrationError integrationError)
            {
                writer.WriteString("code", integrationError.Code);
                writer.WriteString("message", integrationError.Message);
                writer.WriteString("type", integrationError.Type.ToString());
            }
            else
            {
                writer.WriteString("code", "Unknown");
                writer.WriteString("message", error.Message);
                writer.WriteString("type", ErrorType.Failure.ToString());
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    internal static List<IError> ReadErrors(JsonElement root)
    {
        List<IError> errors = new();

        if (!root.TryGetProperty("errors", out JsonElement errorsElement)
            || errorsElement.ValueKind != JsonValueKind.Array)
        {
            return errors;
        }

        foreach (JsonElement errorElement in errorsElement.EnumerateArray())
        {
            string code = errorElement.TryGetProperty("code", out JsonElement codeElement)
                ? codeElement.GetString() ?? "Unknown"
                : "Unknown";

            string message = errorElement.TryGetProperty("message", out JsonElement messageElement)
                ? messageElement.GetString() ?? "Unknown error"
                : "Unknown error";

            ErrorType type = ErrorType.Failure;
            if (errorElement.TryGetProperty("type", out JsonElement typeElement)
                && Enum.TryParse(typeElement.GetString(), out ErrorType parsed))
            {
                type = parsed;
            }

            errors.Add(new IntegrationError(code, message, type));
        }

        return errors;
    }
}
