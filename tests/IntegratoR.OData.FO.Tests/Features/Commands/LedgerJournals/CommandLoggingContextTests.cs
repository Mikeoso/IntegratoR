using FluentAssertions;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Features.Commands.LedgerJournals;

/// <summary>
/// Tests for command logging context methods across all LedgerJournal create and update commands.
/// </summary>
public class CommandLoggingContextTests
{
    private static LedgerJournalHeader BuildHeader(string journalName = "GenJnl", string journalBatchNumber = "GJ001") => new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = journalBatchNumber,
        JournalName = journalName,
        Description = "Test journal"
    };

    private static LedgerJournalLine BuildLine(string journalBatchNumber = "GJ001") => new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = journalBatchNumber,
        LineNumber = 1.0m,
        AccountDisplayValue = "110110-001-023",
        CurrencyCode = "USD",
        TransDate = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Verifies that CreateLedgerJournalHeaderCommand.GetLoggingContext delegates to the entity's GetLoggingContext.
    /// </summary>
    [Fact]
    public void CreateLedgerJournalHeaderCommand_GetLoggingContext_DelegatesToEntity()
    {
        // Arrange
        var header = BuildHeader();
        var command = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(header);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey(nameof(LedgerJournalHeader.DataAreaId));
        context.Should().ContainKey(nameof(LedgerJournalHeader.JournalName));
        context.Should().ContainKey(nameof(LedgerJournalHeader.Description));
    }

    /// <summary>
    /// Verifies that CreateLedgerJournalHeadersCommand.GetLoggingContext returns EntityType, Count, and JournalNames.
    /// </summary>
    [Fact]
    public void CreateLedgerJournalHeadersCommand_GetLoggingContext_ReturnsCountAndJournalNames()
    {
        // Arrange
        var headers = new[]
        {
            BuildHeader("GenJnl1"),
            BuildHeader("GenJnl2")
        };
        var command = new CreateLedgerJournalHeadersCommand<LedgerJournalHeader>(headers);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("EntityType");
        context.Should().ContainKey("Count");
        context.Should().ContainKey("JournalNames");
        context["Count"].Should().Be(2);
        context["JournalNames"].ToString().Should().Contain("GenJnl1");
        context["JournalNames"].ToString().Should().Contain("GenJnl2");
    }

    /// <summary>
    /// Verifies that CreateLedgerJournalLineCommand.GetLoggingContext delegates to the entity's GetLoggingContext.
    /// </summary>
    [Fact]
    public void CreateLedgerJournalLineCommand_GetLoggingContext_DelegatesToEntity()
    {
        // Arrange
        var line = BuildLine();
        var command = new CreateLedgerJournalLineCommand<LedgerJournalLine>(line);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey(nameof(LedgerJournalLine.DataAreaId));
        context.Should().ContainKey(nameof(LedgerJournalLine.JournalBatchNumber));
        context.Should().ContainKey(nameof(LedgerJournalLine.LineNumber));
    }

    /// <summary>
    /// Verifies that CreateLedgerJournalLinesCommand.GetLoggingContext returns Count and JournalNames (values are JournalBatchNumbers).
    /// </summary>
    [Fact]
    public void CreateLedgerJournalLinesCommand_GetLoggingContext_ReturnsCountAndBatchNumbers()
    {
        // Arrange
        var lines = new[]
        {
            BuildLine("GJ001"),
            BuildLine("GJ002")
        };
        var command = new CreateLedgerJournalLinesCommand<LedgerJournalLine>(lines);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Count");
        context.Should().ContainKey("JournalNames");
        context["Count"].Should().Be(2);
        context["JournalNames"].ToString().Should().Contain("GJ001");
        context["JournalNames"].ToString().Should().Contain("GJ002");
    }

    /// <summary>
    /// Verifies that UpdateLedgerJournalHeaderCommand.GetLoggingContext delegates to the entity's GetLoggingContext.
    /// </summary>
    [Fact]
    public void UpdateLedgerJournalHeaderCommand_GetLoggingContext_DelegatesToEntity()
    {
        // Arrange
        var header = BuildHeader();
        var command = new UpdateLedgerJournalHeaderCommand<LedgerJournalHeader>(header);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey(nameof(LedgerJournalHeader.DataAreaId));
        context.Should().ContainKey(nameof(LedgerJournalHeader.JournalName));
        context.Should().ContainKey(nameof(LedgerJournalHeader.Description));
    }

    /// <summary>
    /// Verifies that UpdateLedgerJournalHeadersCommand.GetLoggingContext returns Count and JournalNames.
    /// </summary>
    [Fact]
    public void UpdateLedgerJournalHeadersCommand_GetLoggingContext_ReturnsCountAndJournalNames()
    {
        // Arrange
        var headers = new[]
        {
            BuildHeader("GenJnl1"),
            BuildHeader("GenJnl2")
        };
        var command = new UpdateLedgerJournalHeadersCommand<LedgerJournalHeader>(headers);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Count");
        context.Should().ContainKey("JournalNames");
        context["Count"].Should().Be(2);
        context["JournalNames"].ToString().Should().Contain("GenJnl1");
        context["JournalNames"].ToString().Should().Contain("GenJnl2");
    }

    /// <summary>
    /// Verifies that UpdateLedgerJournalLineCommand.GetLoggingContext delegates to the entity's GetLoggingContext.
    /// </summary>
    [Fact]
    public void UpdateLedgerJournalLineCommand_GetLoggingContext_DelegatesToEntity()
    {
        // Arrange
        var line = BuildLine();
        var command = new UpdateLedgerJournalLineCommand<LedgerJournalLine>(line);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey(nameof(LedgerJournalLine.DataAreaId));
        context.Should().ContainKey(nameof(LedgerJournalLine.JournalBatchNumber));
        context.Should().ContainKey(nameof(LedgerJournalLine.LineNumber));
    }

    /// <summary>
    /// Verifies that UpdateLedgerJournalLinesCommand.GetLoggingContext returns Count and JournalBatchNumbers,
    /// with duplicate batch numbers deduplicated via Distinct.
    /// </summary>
    [Fact]
    public void UpdateLedgerJournalLinesCommand_GetLoggingContext_ReturnsCountAndBatchNumbers_WithDeduplication()
    {
        // Arrange -- two lines with the same batch number to verify Distinct() deduplication
        var lines = new[]
        {
            BuildLine("GJ001"),
            BuildLine("GJ001"),
            BuildLine("GJ002")
        };
        var command = new UpdateLedgerJournalLinesCommand<LedgerJournalLine>(lines);

        // Act
        var context = command.GetLoggingContext();

        // Assert
        context.Should().ContainKey("Count");
        context.Should().ContainKey("JournalBatchNumbers");
        context["Count"].Should().Be(3);
        // GJ001 appears twice in input but should be deduplicated in the output
        var batchNumbers = context["JournalBatchNumbers"].ToString()!;
        var splitNumbers = batchNumbers.Split(',', StringSplitOptions.TrimEntries);
        splitNumbers.Should().HaveCount(2);
        splitNumbers.Should().Contain("GJ001");
        splitNumbers.Should().Contain("GJ002");
    }
}
