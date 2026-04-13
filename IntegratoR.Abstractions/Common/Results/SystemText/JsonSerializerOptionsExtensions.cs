using System.Text.Json;

namespace IntegratoR.Abstractions.Common.Results.SystemText;

/// <summary>
/// Extension methods for registering <see cref="ResultJsonConverterFactory"/> on
/// <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Adds the <see cref="ResultJsonConverterFactory"/> so any closed <c>Result&lt;T&gt;</c>
    /// can be serialised and deserialised through this <see cref="JsonSerializerOptions"/> instance.
    /// </summary>
    /// <param name="options">The options to mutate.</param>
    /// <returns>The same options instance for fluent chaining.</returns>
    public static JsonSerializerOptions AddResultConverters(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new ResultJsonConverterFactory());
        return options;
    }
}
