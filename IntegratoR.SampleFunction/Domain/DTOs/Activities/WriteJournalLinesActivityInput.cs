using IntegratoR.RELion.Domain.Models;

namespace IntegratoR.SampleFunction.Domain.DTOs.Activities;

public class WriteJournalLinesActivityInput
{
    public required string BlobName { get; set; }
    public required List<RelionLedgerJournalLine> Lines { get; set; }
}