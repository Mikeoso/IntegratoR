using IntegratoR.RELion.Domain.Models;

namespace IntegratoR.SampleFunction.Domain.DTOs.Activities
{
    /// <summary>
    /// Represents the root structure of the incoming journal JSON file,
    /// which contains the list of lines within a 'Data' property.
    /// </summary>
    public class JournalFileWrapper
    {
        /// <summary>
        /// The list of journal lines contained in the file.
        /// </summary>
        public required List<RelionLedgerJournalLine> Data { get; set; }
    }
}
