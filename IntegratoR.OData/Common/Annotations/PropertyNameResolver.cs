using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace IntegratoR.OData.Common.Annotations;

/// <summary>
/// Resolves the OData wire name of a CLR member, honouring
/// <see cref="JsonPropertyNameAttribute"/> when present and falling back to the CLR
/// member name otherwise. Results are cached per <see cref="MemberInfo"/> so the
/// reflection lookup happens at most once per (type, member).
/// </summary>
/// <remarks>
/// Used by <see cref="Services.ODataService{TEntity}"/> when building composite-key
/// dictionaries and create/update payloads, and by
/// <c>IntegratoR.OData.Common.Filters.IntegratoRODataExpressionTranslator</c> when
/// translating LINQ filter / select / expand expressions into OData query strings.
/// All three call sites must agree on the same wire name for the same member, so the
/// lookup lives in one place.
/// </remarks>
internal static class PropertyNameResolver
{
    private static readonly ConcurrentDictionary<MemberInfo, string> Cache = new();

    /// <summary>
    /// Returns the OData wire name for <paramref name="member"/>: the value of
    /// <see cref="JsonPropertyNameAttribute.Name"/> if the attribute is present,
    /// otherwise <see cref="MemberInfo.Name"/>.
    /// </summary>
    public static string Resolve(MemberInfo member) =>
        Cache.GetOrAdd(member, static m =>
            m.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? m.Name);
}
