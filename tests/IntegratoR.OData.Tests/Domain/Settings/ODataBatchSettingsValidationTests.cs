using FluentAssertions;
using IntegratoR.OData.Domain.Settings;
using Xunit;

namespace IntegratoR.OData.Tests.Domain.Settings;

/// <summary>
/// Tests the <see cref="ODataSettingsValidator"/> bound on <c>Batch.MaxOperationsPerChunk</c>
/// (1..5000, D365's documented maximum operations per <c>$batch</c>).
/// </summary>
public class ODataBatchSettingsValidationTests
{
    private readonly ODataSettingsValidator _sut = new();

    private static ODataSettings ValidBase() => new()
    {
        Url = "https://test.operations.dynamics.com/data",
        Authentication = new ODataAuthenticationSettings
        {
            Mode = AuthenticationMode.OAuth,
            OAuth = new ODataOAuthSettings { ClientId = "c", ClientSecret = "s", TenantId = "t", Resource = "r" },
        },
    };

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5001)]
    public void Validate_MaxOperationsPerChunkOutOfRange_Fails(int value)
    {
        ODataSettings settings = ValidBase();
        settings.Batch.MaxOperationsPerChunk = value;

        var result = _sut.Validate(null, settings);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxOperationsPerChunk");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(150)]
    [InlineData(5000)]
    public void Validate_MaxOperationsPerChunkInRange_Passes(int value)
    {
        ODataSettings settings = ValidBase();
        settings.Batch.MaxOperationsPerChunk = value;

        var result = _sut.Validate(null, settings);

        result.Succeeded.Should().BeTrue();
    }
}
