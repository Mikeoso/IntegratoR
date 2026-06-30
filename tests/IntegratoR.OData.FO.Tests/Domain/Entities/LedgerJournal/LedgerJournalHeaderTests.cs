using FluentAssertions;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Domain.Entities.LedgerJournal;

/// <summary>
/// Tests for <see cref="LedgerJournalHeader"/> entity composite key and logging context.
/// </summary>
public class LedgerJournalHeaderTests
{
    /// <summary>
    /// Verifies that GetCompositeKey returns DataAreaId and JournalBatchNumber when both are set.
    /// </summary>
    [Fact]
    public void GetCompositeKey_AllFieldsSet_ReturnsDataAreaIdAndJournalBatchNumber()
    {
        // Arrange
        var header = new LedgerJournalHeader
        {
            DataAreaId = "USMF",
            JournalBatchNumber = "GJ001",
            JournalName = "GenJnl",
            Description = "Test journal"
        };

        // Act
        var key = header.GetCompositeKey();

        // Assert
        key.Should().Equal("USMF", "GJ001");
    }

    /// <summary>
    /// Verifies that GetCompositeKey returns a real null element (not the literal string "null")
    /// when JournalBatchNumber is null. A header without a batch number has no identity yet;
    /// <see cref="IntegratoR.OData.Common.Services.ODataService{TEntity}"/> turns the null element
    /// into a Validation failure rather than writing against a non-existent batch named "null".
    /// </summary>
    [Fact]
    public void GetCompositeKey_NullJournalBatchNumber_ReturnsNullElementNotLiteralString()
    {
        // Arrange
        var header = new LedgerJournalHeader
        {
            DataAreaId = "USMF",
            JournalBatchNumber = null,
            JournalName = "GenJnl",
            Description = "Test journal"
        };

        // Act
        var key = header.GetCompositeKey();

        // Assert
        key.Should().HaveCount(2);
        key[1].Should().BeNull();
        key[1].Should().NotBe("null");
    }

    /// <summary>
    /// Verifies that GetLoggingContext returns a dictionary containing all public properties.
    /// </summary>
    [Fact]
    public void GetLoggingContext_ReturnsAllPublicProperties()
    {
        // Arrange
        var header = new LedgerJournalHeader
        {
            DataAreaId = "USMF",
            JournalBatchNumber = "GJ001",
            JournalName = "GenJnl",
            Description = "Test journal"
        };

        // Act
        var context = header.GetLoggingContext();

        // Assert
        context.Should().ContainKey(nameof(LedgerJournalHeader.DataAreaId));
        context.Should().ContainKey(nameof(LedgerJournalHeader.JournalBatchNumber));
        context.Should().ContainKey(nameof(LedgerJournalHeader.JournalName));
        context.Should().ContainKey(nameof(LedgerJournalHeader.Description));
    }
}
