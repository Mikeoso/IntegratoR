using System.Text.Json.Serialization;
using FluentAssertions;
using IntegratoR.OData.Common.Services;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// Pins the shared <see cref="ODataClientAdapter.CaseInsensitiveOptions"/> configuration used by
/// the adapter's <c>DeserializeResponse&lt;TEntity&gt;</c> code path. D365 F&amp;O OData v4 serialises
/// enum values as string names (e.g. <c>"PostingLayer": "Current"</c>), so the shared options must
/// include a <see cref="JsonStringEnumConverter"/> — otherwise every Create/Update response
/// carrying an enum property would throw on round-trip.
/// </summary>
public class ODataClientAdapterJsonOptionsTests
{
    [Fact]
    public void CaseInsensitiveOptions_RegistersJsonStringEnumConverter()
    {
        ODataClientAdapter.CaseInsensitiveOptions.Converters
            .Should().ContainSingle(c => c is JsonStringEnumConverter);
    }

    [Fact]
    public void CaseInsensitiveOptions_StillHonoursPropertyNameCaseInsensitive()
    {
        // Pins that adding the enum converter did not regress the pre-existing
        // PropertyNameCaseInsensitive = true setting on the same options instance.
        ODataClientAdapter.CaseInsensitiveOptions.PropertyNameCaseInsensitive.Should().BeTrue();
    }
}
