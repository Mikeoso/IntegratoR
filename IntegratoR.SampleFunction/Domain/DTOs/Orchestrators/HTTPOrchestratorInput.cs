using Newtonsoft.Json;

namespace IntegratoR.SampleFunction.Domain.DTOs.Orchestrators
{
    public class HTTPOrchestratorInput
    {
        [JsonProperty("BusinessEventId")]
        public required string BusinessEventId { get; set; }

        [JsonProperty("BusinessEventLegalEntity")]
        public required string BusinessEventLegalEntity { get; set; }

        [JsonProperty("EventTime")]
        public DateTime EventTime { get; set; }

        [JsonProperty("ImportDate")]
        public DateTime ImportDate { get; set; }

        [JsonProperty("EventId")]
        public Guid EventId { get; set; }
    }
}
