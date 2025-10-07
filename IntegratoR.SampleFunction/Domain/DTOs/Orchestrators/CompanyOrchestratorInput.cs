using IntegratoR.RELion.Domain.Models;

namespace IntegratoR.SampleFunction.Domain.DTOs.Orchestrators
{
    public class CompanyOrchestratorInput
    {
        public required string Company { get; set; }
        public required List<RelionLedgerJournalLine> Lines { get; set; }
    }
}
