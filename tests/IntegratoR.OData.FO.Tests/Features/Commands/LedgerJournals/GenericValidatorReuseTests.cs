using FluentAssertions;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalLine;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalHeader;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.UpdateLedgerJournalLine;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Features.Commands.LedgerJournals;

/// <summary>
/// Proves two things for the FO LedgerJournal commands after PR-C:
/// <list type="number">
/// <item>Each FO single/batch command IS-A generic base command (compile-gated assignments),
/// so the generic command pipeline applies to them.</item>
/// <item>The thin derived per-command validator runs the baseline rule for the concrete FO
/// command type — which is what the MediatR <c>ValidationBehaviour</c> resolves and executes
/// (the container resolves <c>IValidator&lt;ConcreteCommand&gt;</c> by exact closed type, so the
/// generic base validator alone would never fire for the derived command).</item>
/// </list>
/// </summary>
public class GenericValidatorReuseTests
{
    private static LedgerJournalLine BuildLine() => new()
    {
        DataAreaId = "USMF",
        JournalBatchNumber = "GJ001",
        LineNumber = 1.0m,
        AccountDisplayValue = "110110-001-023",
        CurrencyCode = "USD",
        TransDate = DateTimeOffset.UtcNow
    };

    private static LedgerJournalHeader BuildHeader() => new()
    {
        DataAreaId = "USMF",
        JournalName = "GenJnl",
        Description = "Test journal"
    };

    [Fact]
    public void CreateLedgerJournalLineCommand_IsA_CreateCommand_AndDerivedValidatorFlagsNullEntity()
    {
        // IS-A: this assignment ONLY compiles because the FO command now inherits CreateCommand<T>.
        CreateCommand<LedgerJournalLine> asBase = new CreateLedgerJournalLineCommand<LedgerJournalLine>(null!);
        asBase.Should().NotBeNull();

        var validator = new CreateLedgerJournalLineCommandValidator<LedgerJournalLine>();

        var invalid = validator.Validate(new CreateLedgerJournalLineCommand<LedgerJournalLine>(null!));
        invalid.IsValid.Should().BeFalse();
        invalid.Errors.Should().ContainSingle(e => e.PropertyName == "Entity");

        var valid = validator.Validate(new CreateLedgerJournalLineCommand<LedgerJournalLine>(BuildLine()));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateLedgerJournalHeaderCommand_IsA_CreateCommand_AndDerivedValidatorFlagsNullEntity()
    {
        CreateCommand<LedgerJournalHeader> asBase = new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(null!);
        asBase.Should().NotBeNull();

        var validator = new CreateLedgerJournalHeaderCommandValidator<LedgerJournalHeader>();

        var invalid = validator.Validate(new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(null!));
        invalid.IsValid.Should().BeFalse();
        invalid.Errors.Should().ContainSingle(e => e.PropertyName == "Entity");

        var valid = validator.Validate(new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(BuildHeader()));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateLedgerJournalLineCommand_IsA_UpdateCommand_AndDerivedValidatorFlagsNullEntity()
    {
        UpdateCommand<LedgerJournalLine> asBase = new UpdateLedgerJournalLineCommand<LedgerJournalLine>(null!);
        asBase.Should().NotBeNull();

        var validator = new UpdateLedgerJournalLineCommandValidator<LedgerJournalLine>();

        var invalid = validator.Validate(new UpdateLedgerJournalLineCommand<LedgerJournalLine>(null!));
        invalid.IsValid.Should().BeFalse();
        invalid.Errors.Should().ContainSingle(e => e.PropertyName == "Entity");

        var valid = validator.Validate(new UpdateLedgerJournalLineCommand<LedgerJournalLine>(BuildLine()));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateLedgerJournalHeaderCommand_IsA_UpdateCommand_AndDerivedValidatorFlagsNullEntity()
    {
        UpdateCommand<LedgerJournalHeader> asBase = new UpdateLedgerJournalHeaderCommand<LedgerJournalHeader>(null!);
        asBase.Should().NotBeNull();

        var validator = new UpdateLedgerJournalHeaderCommandValidator<LedgerJournalHeader>();

        var invalid = validator.Validate(new UpdateLedgerJournalHeaderCommand<LedgerJournalHeader>(null!));
        invalid.IsValid.Should().BeFalse();
        invalid.Errors.Should().ContainSingle(e => e.PropertyName == "Entity");

        var valid = validator.Validate(new UpdateLedgerJournalHeaderCommand<LedgerJournalHeader>(BuildHeader()));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateLedgerJournalLinesCommand_IsA_CreateBatchCommand_AndDerivedValidatorFlagsEmptyBatch()
    {
        CreateBatchCommand<LedgerJournalLine> asBase =
            new CreateLedgerJournalLinesCommand<LedgerJournalLine>([]);
        asBase.Should().NotBeNull();

        var validator = new CreateLedgerJournalLinesCommandValidator<LedgerJournalLine>();

        var empty = validator.Validate(new CreateLedgerJournalLinesCommand<LedgerJournalLine>([]));
        empty.IsValid.Should().BeFalse();
        empty.Errors.Should().NotBeEmpty();

        var valid = validator.Validate(new CreateLedgerJournalLinesCommand<LedgerJournalLine>([BuildLine()]));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateLedgerJournalHeadersCommand_IsA_UpdateBatchCommand_AndDerivedValidatorFlagsEmptyBatch()
    {
        UpdateBatchCommand<LedgerJournalHeader> asBase =
            new UpdateLedgerJournalHeadersCommand<LedgerJournalHeader>([]);
        asBase.Should().NotBeNull();

        var validator = new UpdateLedgerJournalHeadersCommandValidator<LedgerJournalHeader>();

        var empty = validator.Validate(new UpdateLedgerJournalHeadersCommand<LedgerJournalHeader>([]));
        empty.IsValid.Should().BeFalse();
        empty.Errors.Should().NotBeEmpty();

        var valid = validator.Validate(new UpdateLedgerJournalHeadersCommand<LedgerJournalHeader>([BuildHeader()]));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateLedgerJournalHeadersCommand_IsA_CreateBatchCommand_AndDerivedValidatorFlagsEmptyBatch()
    {
        CreateBatchCommand<LedgerJournalHeader> asBase =
            new CreateLedgerJournalHeadersCommand<LedgerJournalHeader>([]);
        asBase.Should().NotBeNull();

        var validator = new CreateLedgerJournalHeadersCommandValidator<LedgerJournalHeader>();

        var empty = validator.Validate(new CreateLedgerJournalHeadersCommand<LedgerJournalHeader>([]));
        empty.IsValid.Should().BeFalse();
        empty.Errors.Should().NotBeEmpty();

        var valid = validator.Validate(new CreateLedgerJournalHeadersCommand<LedgerJournalHeader>([BuildHeader()]));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateLedgerJournalLinesCommand_IsA_UpdateBatchCommand_AndDerivedValidatorFlagsEmptyBatch()
    {
        UpdateBatchCommand<LedgerJournalLine> asBase =
            new UpdateLedgerJournalLinesCommand<LedgerJournalLine>([]);
        asBase.Should().NotBeNull();

        var validator = new UpdateLedgerJournalLinesCommandValidator<LedgerJournalLine>();

        var empty = validator.Validate(new UpdateLedgerJournalLinesCommand<LedgerJournalLine>([]));
        empty.IsValid.Should().BeFalse();
        empty.Errors.Should().NotBeEmpty();

        var valid = validator.Validate(new UpdateLedgerJournalLinesCommand<LedgerJournalLine>([BuildLine()]));
        valid.IsValid.Should().BeTrue();
    }
}
