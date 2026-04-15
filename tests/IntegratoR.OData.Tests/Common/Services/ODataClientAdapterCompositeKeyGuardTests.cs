using FluentAssertions;
using IntegratoR.OData.Common.Services;
using IntegratoR.TestKit.Doubles.Entities;
using PanoramicData.OData.Client;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// Pins the composite-key input-validation guards in
/// <see cref="ODataClientAdapter.FindByKeyAsync{TEntity}"/>. Framework callers obtain key names
/// from entity reflection via <c>PropertyNameResolver</c>, so these guards protect against a
/// consumer bypassing <c>ODataService</c> and passing the adapter a tainted dictionary.
/// </summary>
public class ODataClientAdapterCompositeKeyGuardTests
{
    private static ODataClientAdapter CreateAdapter()
    {
        // A minimal ODataClient is enough for the guard tests — the guards throw synchronously
        // before any HTTP traffic is issued, so the HttpClient wiring is irrelevant here.
        var httpClient = new HttpClient { BaseAddress = new Uri("https://unused.example.com/") };
        var options = new ODataClientOptions { BaseUrl = "https://unused.example.com/", HttpClient = httpClient };
        return new ODataClientAdapter(new ODataClient(options));
    }

    [Fact]
    public async Task FindByKeyAsync_EmptyCompositeKeyDictionary_ThrowsArgumentException()
    {
        // Arrange
        var adapter = CreateAdapter();
        var emptyKey = new Dictionary<string, object>();

        // Act
        Func<Task> act = () => adapter.FindByKeyAsync<TestEntity>("TestEntities", emptyKey);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*must contain at least one key field*");
    }

    [Theory]
    // Injection attempt: trailing OData clause.
    [InlineData("JournalNum eq '1' or 1 eq 1")]
    // Injection attempt: embedded space.
    [InlineData("field with space")]
    // Injection attempt: leading digit.
    [InlineData("1field")]
    // Injection attempt: special character.
    [InlineData("field;drop")]
    // Injection attempt: empty segment.
    [InlineData("")]
    public async Task FindByKeyAsync_InvalidKeyFieldName_ThrowsArgumentException(string taintedKey)
    {
        // Arrange
        var adapter = CreateAdapter();
        var compositeKey = new Dictionary<string, object>
        {
            [taintedKey] = "value"
        };

        // Act
        Func<Task> act = () => adapter.FindByKeyAsync<TestEntity>("TestEntities", compositeKey);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*is not a valid OData property identifier*");
    }

    [Theory]
    // Legitimate D365 camelCase wire names.
    [InlineData("dataAreaId")]
    [InlineData("JournalBatchNumber")]
    // Qualified namespace (dot) — allowed by the whitelist.
    [InlineData("Contoso.JournalNum")]
    // Leading underscore — allowed by the whitelist.
    [InlineData("_internal")]
    public void IsValidODataFieldName_LegitimateKeys_AreAccepted(string legitimateKey)
    {
        // Arrange — use reflection to call the private helper directly so we pin the
        // whitelist independently of the async guard path.
        var method = typeof(ODataClientAdapter).GetMethod(
            "IsValidODataFieldName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull("IsValidODataFieldName must exist as a private static helper");

        // Act
        bool result = (bool)method!.Invoke(null, new object[] { legitimateKey })!;

        // Assert
        result.Should().BeTrue();
    }
}
