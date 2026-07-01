using FluentResults;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// Newtonsoft.Json converter for the non-generic <see cref="Result"/> type.
/// Property names and the IError ↔ primitives mapping are owned by <see cref="ResultJsonShape"/>
/// so this converter stays in lockstep with the System.Text.Json equivalent.
/// </summary>
public class ResultJsonConverter : JsonConverter<Result>
{
    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, Result? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName(ResultJsonShape.IsSuccess);
        writer.WriteValue(value.IsSuccess);

        if (!value.IsSuccess)
        {
            writer.WritePropertyName(ResultJsonShape.Errors);
            NewtonsoftResultErrorSerializer.Write(writer, value.Errors);
        }

        writer.WriteEndObject();
    }

    /// <inheritdoc/>
    public override Result? ReadJson(JsonReader reader, Type objectType, Result? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        var obj = JObject.Load(reader);
        var isSuccess = obj[ResultJsonShape.IsSuccess]?.Value<bool>() ?? false;

        if (isSuccess)
            return Result.Ok();

        IReadOnlyList<IError> errors = NewtonsoftResultErrorSerializer.Read(obj[ResultJsonShape.Errors]);
        if (errors.Count == 0)
            return Result.Fail(ResultJsonShape.MissingErrorsError());

        return Result.Fail(errors);
    }
}

/// <summary>
/// Newtonsoft.Json converter that handles all <see cref="Result{T}"/> types dynamically.
/// Registers once and handles any generic <c>Result&lt;T&gt;</c> via reflection.
/// </summary>
public class ResultGenericJsonConverter : JsonConverter
{
    /// <inheritdoc/>
    public override bool CanConvert(Type objectType)
    {
        return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Result<>);
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var resultType = value.GetType();
        var isSuccess = (bool)resultType.GetProperty(nameof(Result.IsSuccess))!.GetValue(value)!;
        var errors = (IReadOnlyList<IError>)resultType.GetProperty(nameof(Result.Errors))!.GetValue(value)!;

        writer.WriteStartObject();
        writer.WritePropertyName(ResultJsonShape.IsSuccess);
        writer.WriteValue(isSuccess);

        if (isSuccess)
        {
            var valueProperty = resultType.GetProperty(nameof(Result<object>.Value))!;
            var innerValue = valueProperty.GetValue(value);
            writer.WritePropertyName(ResultJsonShape.Value);
            serializer.Serialize(writer, innerValue);
        }
        else
        {
            writer.WritePropertyName(ResultJsonShape.Errors);
            NewtonsoftResultErrorSerializer.Write(writer, errors);
        }

        writer.WriteEndObject();
    }

    /// <inheritdoc/>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        var obj = JObject.Load(reader);
        var isSuccess = obj[ResultJsonShape.IsSuccess]?.Value<bool>() ?? false;
        var valueType = objectType.GetGenericArguments()[0];

        var failMethod = typeof(Result).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .First(m => m.Name == "Fail" && m.IsGenericMethod && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(IEnumerable<IError>))
            .MakeGenericMethod(valueType);

        if (isSuccess)
        {
            if (!obj.TryGetValue(ResultJsonShape.Value, out JToken? valueToken))
            {
                // Corrupted payload: success but no value field. Convert to a failure rather
                // than silently materialising default(T).
                return failMethod.Invoke(null, [new IError[] { ResultJsonShape.MissingValueError() }]);
            }

            object? value;
            if (valueToken.Type == JTokenType.Null)
            {
                // Explicit null on a non-nullable value type (e.g. Result<int>) is corruption,
                // not a legitimate "success carrying default(T)". Symmetric with the
                // missing-value branch above.
                bool isNonNullableValueType = valueType.IsValueType
                    && Nullable.GetUnderlyingType(valueType) is null;
                if (isNonNullableValueType)
                {
                    return failMethod.Invoke(null, [new IError[] { ResultJsonShape.MissingValueError() }]);
                }

                value = null;
            }
            else
            {
                value = valueToken.ToObject(valueType, serializer);
            }

            var okMethod = typeof(Result).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .First(m => m.Name == "Ok" && m.IsGenericMethod && m.GetParameters().Length == 1)
                .MakeGenericMethod(valueType);
            return okMethod.Invoke(null, [value]);
        }

        IReadOnlyList<IError> errors = NewtonsoftResultErrorSerializer.Read(obj[ResultJsonShape.Errors]);
        if (errors.Count == 0)
            errors = new IError[] { ResultJsonShape.MissingErrorsError() };

        return failMethod.Invoke(null, [errors]);
    }
}

/// <summary>
/// Newtonsoft.Json converter for the generic <see cref="Result{T}"/> type.
/// Serialises <c>isSuccess</c>, <c>value</c>, and <c>errors</c>.
/// </summary>
/// <typeparam name="T">The value type carried by the <see cref="Result{T}"/>.</typeparam>
public class ResultJsonConverter<T> : JsonConverter<Result<T>>
{
    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, Result<T>? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName(ResultJsonShape.IsSuccess);
        writer.WriteValue(value.IsSuccess);

        if (value.IsSuccess)
        {
            writer.WritePropertyName(ResultJsonShape.Value);
            serializer.Serialize(writer, value.Value);
        }
        else
        {
            writer.WritePropertyName(ResultJsonShape.Errors);
            NewtonsoftResultErrorSerializer.Write(writer, value.Errors);
        }

        writer.WriteEndObject();
    }

    /// <inheritdoc/>
    public override Result<T>? ReadJson(JsonReader reader, Type objectType, Result<T>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        var obj = JObject.Load(reader);
        var isSuccess = obj[ResultJsonShape.IsSuccess]?.Value<bool>() ?? false;

        if (isSuccess)
        {
            if (!obj.TryGetValue(ResultJsonShape.Value, out JToken? valueToken))
            {
                // Corrupted payload: success but no value field. Convert to a failure with a
                // synthetic error rather than silently returning Result.Ok(default(T)).
                return Result.Fail<T>(ResultJsonShape.MissingValueError());
            }

            if (valueToken.Type == JTokenType.Null)
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

            T? value = valueToken.ToObject<T>(serializer);
            return Result.Ok(value!);
        }

        IReadOnlyList<IError> errors = NewtonsoftResultErrorSerializer.Read(obj[ResultJsonShape.Errors]);
        if (errors.Count == 0)
            return Result.Fail<T>(ResultJsonShape.MissingErrorsError());

        return Result.Fail<T>(errors);
    }
}

/// <summary>
/// Newtonsoft.Json plumbing for the errors array. Lives outside <see cref="ResultJsonConverter{T}"/>
/// so it isn't duplicated per closed <c>T</c>, and delegates to <see cref="ResultJsonShape"/> for
/// the actual IError ↔ primitives mapping.
/// </summary>
internal static class NewtonsoftResultErrorSerializer
{
    public static void Write(JsonWriter writer, IReadOnlyList<IError> errors)
    {
        writer.WriteStartArray();
        foreach (IError error in errors)
        {
            (string code, string message, ErrorType type) = ResultJsonShape.Project(error);

            writer.WriteStartObject();
            writer.WritePropertyName(ResultJsonShape.Code);
            writer.WriteValue(code);
            writer.WritePropertyName(ResultJsonShape.Message);
            writer.WriteValue(message);
            writer.WritePropertyName(ResultJsonShape.Type);
            writer.WriteValue(type.ToString());
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    public static IReadOnlyList<IError> Read(JToken? errorsToken)
    {
        if (errorsToken is not JArray arr)
            return Array.Empty<IError>();

        List<IError> errors = new(arr.Count);
        foreach (var item in arr)
        {
            errors.Add(ResultJsonShape.Hydrate(
                item[ResultJsonShape.Code]?.Value<string>(),
                item[ResultJsonShape.Message]?.Value<string>(),
                item[ResultJsonShape.Type]?.Value<string>()));
        }

        return errors;
    }
}
