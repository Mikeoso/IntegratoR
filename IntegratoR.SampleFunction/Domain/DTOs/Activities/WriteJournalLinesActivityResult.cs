using Microsoft.Azure.Functions.Worker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegratoR.SampleFunction.Domain.DTOs.Activities
{
    public class WriteJournalLinesActivityResult
    {
        [BlobOutput("input/{BlobName}", Connection = "AzureWebJobsStorage")]
        public required byte[] BlobContent { get; set; }

        public required string BlobName { get; set; }
    }
}
