using FluentAssertions;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Domain.Entities.LedgerJournal;

/// <summary>
/// Tests for <see cref="LedgerJournalLine"/> entity composite key and logging context.
/// </summary>
public class LedgerJournalLineTests
{
    /// <summary>
    /// Verifies that GetCompositeKey returns a three-part key with DataAreaId, JournalBatchNumber, and LineNumber.
    /// </summary>
    [Fact]
    public void GetCompositeKey_AllFieldsSet_ReturnsThreePartKey()
    {
        // Arrange
        var line = new LedgerJournalLine
        {
            DataAreaId = "USMF",
            JournalBatchNumber = "GJ001",
            LineNumber = 1.0m,
            AccountDisplayValue = "110110-001-023",
            CurrencyCode = "USD",
            TransDate = DateTimeOffset.UtcNow
        };

        // Act
        var key = line.GetCompositeKey();

        // Assert
        key.Should().HaveCount(3);
        key[0].Should().Be("USMF");
        key[1].Should().Be("GJ001");
        key[2].Should().Be(1.0m);
    }

    /// <summary>
    /// Verifies that GetLoggingContext returns a dictionary containing expected property names.
    /// </summary>
    [Fact]
    public void GetLoggingContext_ReturnsAllPublicProperties()
    {
        // Arrange
        var line = new LedgerJournalLine
        {
            DataAreaId = "USMF",
            JournalBatchNumber = "GJ001",
            LineNumber = 1.0m,
            AccountDisplayValue = "110110-001-023",
            CurrencyCode = "USD",
            TransDate = DateTimeOffset.UtcNow
        };

        // Act
        var context = line.GetLoggingContext();

        // Assert
        context.Should().ContainKey(nameof(LedgerJournalLine.DataAreaId));
        context.Should().ContainKey(nameof(LedgerJournalLine.JournalBatchNumber));
        context.Should().ContainKey(nameof(LedgerJournalLine.LineNumber));
        context.Should().ContainKey(nameof(LedgerJournalLine.CurrencyCode));
    }
}
