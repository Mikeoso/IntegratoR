using FluentResults;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// Newtonsoft.Json converter for the non-generic <see cref="Result"/> type.
/// Serializes <c>isSuccess</c> and <c>errors</c> (array of code/message/type objects).
/// </summary>
public class ResultJsonConverter : JsonConverter<Result>
{
    public override void WriteJson(JsonWriter writer, Result? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("isSuccess");
        writer.WriteValue(value.IsSuccess);
        writer.WritePropertyName("errors");
        WriteErrors(writer, value.Errors);
        writer.WriteEndObject();
    }

    public override Result? ReadJson(JsonReader reader, Type objectType, Result? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var isSuccess = obj["isSuccess"]?.Value<bool>() ?? false;

        if (isSuccess)
            return Result.Ok();

        var errors = ReadErrors(obj["errors"]);
        return Result.Fail(errors);
    }

    internal static void WriteErrors(JsonWriter writer, IReadOnlyList<IError> errors)
    {
        writer.WriteStartArray();
        foreach (var error in errors)
        {
            writer.WriteStartObject();
            if (error is IntegrationError ie)
            {
                writer.WritePropertyName("code");
                writer.WriteValue(ie.Code);
                writer.WritePropertyName("message");
                writer.WriteValue(ie.Message);
                writer.WritePropertyName("type");
                writer.WriteValue(ie.Type.ToString());
            }
            else
            {
                writer.WritePropertyName("code");
                writer.WriteValue("Unknown");
                writer.WritePropertyName("message");
                writer.WriteValue(error.Message);
                writer.WritePropertyName("type");
                writer.WriteValue(ErrorType.Failure.ToString());
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    internal static List<IError> ReadErrors(JToken? errorsToken)
    {
        var errors = new List<IError>();
        if (errorsToken is not JArray arr)
            return errors;

        foreach (var item in arr)
        {
            var code = item["code"]?.Value<string>() ?? "Unknown";
            var message = item["message"]?.Value<string>() ?? "Unknown error";
            var typeStr = item["type"]?.Value<string>() ?? "Failure";
            var type = Enum.TryParse<ErrorType>(typeStr, out var parsed) ? parsed : ErrorType.Failure;

            errors.Add(new IntegrationError(code, message, type));
        }

        return errors;
    }
}

/// <summary>
/// Newtonsoft.Json converter that handles all <see cref="Result{T}"/> types dynamically.
/// Registers once and handles any generic <c>Result&lt;T&gt;</c> via reflection.
/// </summary>
public class ResultGenericJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Result<>);
    }

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
        writer.WritePropertyName("isSuccess");
        writer.WriteValue(isSuccess);

        if (isSuccess)
        {
            var valueProperty = resultType.GetProperty(nameof(Result<object>.Value))!;
            var innerValue = valueProperty.GetValue(value);
            writer.WritePropertyName("value");
            serializer.Serialize(writer, innerValue);
        }

        writer.WritePropertyName("errors");
        ResultJsonConverter.WriteErrors(writer, errors);
        writer.WriteEndObject();
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var isSuccess = obj["isSuccess"]?.Value<bool>() ?? false;
        var valueType = objectType.GetGenericArguments()[0];

        if (isSuccess)
        {
            var valueToken = obj["value"];
            var value = valueToken is not null
                ? valueToken.ToObject(valueType, serializer)
                : null;

            // Call Result.Ok<T>(value) via reflection
            var okMethod = typeof(Result).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .First(m => m.Name == "Ok" && m.IsGenericMethod && m.GetParameters().Length == 1)
                .MakeGenericMethod(valueType);
            return okMethod.Invoke(null, [value]);
        }

        var errors = ResultJsonConverter.ReadErrors(obj["errors"]);

        // Call Result.Fail<T>(errors) via reflection
        var failMethod = typeof(Result).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .First(m => m.Name == "Fail" && m.IsGenericMethod && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(IEnumerable<IError>))
            .MakeGenericMethod(valueType);
        return failMethod.Invoke(null, [errors]);
    }
}

/// <summary>
/// Newtonsoft.Json converter for the generic <see cref="Result{T}"/> type.
/// Serializes <c>isSuccess</c>, <c>value</c>, and <c>errors</c>.
/// </summary>
public class ResultJsonConverter<T> : JsonConverter<Result<T>>
{
    public override void WriteJson(JsonWriter writer, Result<T>? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("isSuccess");
        writer.WriteValue(value.IsSuccess);

        if (value.IsSuccess)
        {
            writer.WritePropertyName("value");
            serializer.Serialize(writer, value.Value);
        }

        writer.WritePropertyName("errors");
        ResultJsonConverter.WriteErrors(writer, value.Errors);
        writer.WriteEndObject();
    }

    public override Result<T>? ReadJson(JsonReader reader, Type objectType, Result<T>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var isSuccess = obj["isSuccess"]?.Value<bool>() ?? false;

        if (isSuccess)
        {
            var valueToken = obj["value"];
            var value = valueToken is not null
                ? valueToken.ToObject<T>(serializer)
                : default;

            return Result.Ok(value!);
        }

        var errors = ResultJsonConverter.ReadErrors(obj["errors"]);
        return Result.Fail<T>(errors);
    }
}
