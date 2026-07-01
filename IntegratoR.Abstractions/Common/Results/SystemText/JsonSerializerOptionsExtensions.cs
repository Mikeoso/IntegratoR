using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntegratoR.Abstractions.Common.Results.SystemText;

/// <summary>
/// Extension methods for registering <see cref="ResultJsonConverterFactory"/> on
/// <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Adds the <see cref="ResultJsonConverterFactory"/> and the non-generic
    /// <see cref="NonGenericResultJsonConverter"/> so any <c>Result</c> or closed
    /// <c>Result&lt;T&gt;</c> can be serialised through this <see cref="JsonSerializerOptions"/>
    /// instance.
    /// </summary>
    /// <param name="options">The options to mutate.</param>
    /// <returns>The same <paramref name="options"/> instance for fluent chaining.</returns>
    /// <remarks>Idempotent — repeated calls on the same options instance add no duplicate converters.</remarks>
    public static JsonSerializerOptions AddResultConverters(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        bool hasFactory = false;
        bool hasNonGeneric = false;
        foreach (JsonConverter converter in options.Converters)
        {
            if (converter is ResultJsonConverterFactory)
            {
                hasFactory = true;
            }
            else if (converter is NonGenericResultJsonConverter)
            {
                hasNonGeneric = true;
            }
        }

        if (!hasFactory)
        {
            options.Converters.Add(new ResultJsonConverterFactory());
        }
        if (!hasNonGeneric)
        {
            options.Converters.Add(new NonGenericResultJsonConverter());
        }

        return options;
    }
}
