using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IntegratoR.SampleFunction.Domain.Entities.LedgerJournal
{
    [Table("LedgerJournalLines")]
    public class LedgerJournalLineExtension : LedgerJournalLine
    {
        /// <summary>
        /// The custom sales tax code for the integration.
        /// </summary>
        [JsonPropertyName("INWTaxCode")]
        public string? INWTaxCode { get; set; }

        /// <summary>
        /// The record ID (RecId) of a related custom tax transaction record, used for linking integration-specific tax data.
        /// </summary>
        [Required]
        [JsonPropertyName("INWTaxTransRecId")]
        public long INWTaxTransRecId { get; set; }
    }
}
