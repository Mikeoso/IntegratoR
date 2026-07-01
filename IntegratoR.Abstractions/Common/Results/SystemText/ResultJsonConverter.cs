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
/// The non-generic <see cref="Result"/> type is handled separately by
/// <see cref="NonGenericResultJsonConverter"/>. Both are registered together by
/// <see cref="JsonSerializerOptionsExtensions.AddResultConverters"/>.
/// </remarks>
public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType
            && !typeToConvert.IsGenericTypeDefinition
            && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);
    }

    /// <inheritdoc/>
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
    /// <inheritdoc/>
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
            if (!root.TryGetProperty(ResultJsonShape.Value, out JsonElement valueElement))
            {
                // Corrupted payload: success but no value field. Convert to a failure with a
                // synthetic error rather than silently returning Result.Ok(default(T)).
                return Result.Fail<T>(ResultJsonShape.MissingValueError());
            }

            if (valueElement.ValueKind == JsonValueKind.Null)
            {
                // Explicit null on a non-nullable value type (e.g. Result<int>) is corruption,
                // not a legitimate "success carrying default(T)". Symmetric with the
                // missing-value branch above.
                if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null)
                {
                    return Result.Fail<T>(ResultJsonShape.MissingValueError());
                }

                return Result.Ok(default(T)!);
            }

            T? value = valueElement.Deserialize<T>(options);
            return Result.Ok(value!);
        }

        IReadOnlyList<IError> errors = StjResultErrorSerializer.Read(root);
        if (errors.Count == 0)
        {
            return Result.Fail<T>(ResultJsonShape.MissingErrorsError());
        }

        return Result.Fail<T>(errors);
    }

    /// <inheritdoc/>
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
/// System.Text.Json converter for the non-generic <see cref="Result"/> type. Required because
/// some Durable Functions activities and orchestrators in this codebase return the non-generic
/// <c>Result</c> (e.g. <c>context.CallActivityAsync&lt;Result&gt;(...)</c>), and the closed-generic
/// factory <see cref="ResultJsonConverterFactory"/> only handles <see cref="Result{T}"/>.
/// </summary>
public sealed class NonGenericResultJsonConverter : JsonConverter<Result>
{
    /// <inheritdoc/>
    public override Result? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            return Result.Ok();
        }

        IReadOnlyList<IError> errors = StjResultErrorSerializer.Read(root);
        if (errors.Count == 0)
        {
            return Result.Fail(ResultJsonShape.MissingErrorsError());
        }

        return Result.Fail(errors);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean(ResultJsonShape.IsSuccess, value.IsSuccess);

        if (!value.IsSuccess)
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

    public static IReadOnlyList<IError> Read(JsonElement root)
    {
        if (!root.TryGetProperty(ResultJsonShape.Errors, out JsonElement errorsElement)
            || errorsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<IError>();
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
