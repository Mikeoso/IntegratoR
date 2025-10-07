using IntegratoR.RELion.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegratoR.SampleFunction.Domain.DTOs.Activities
{
    public class MapLinesActivityInput
    {
        public required string JournalBatchNumber { get; set; }
        public required List<RelionLedgerJournalLine> Lines { get; set; }
    }
}
