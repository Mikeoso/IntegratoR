namespace IntegratoR.SampleFunction.Domain.DTOs.Orchestrators
{
    /// <summary>
    /// Represents the data required to start the journal file processing orchestration.
    /// </summary>
    public class BlobOrchestratorInput
    {
        /// <summary>
        /// Name of the specified file/folder
        /// </summary>
        public required string BlobName { get; set; }

        /// <summary>
        /// The file content as a byte array.
        /// </summary>
        public required byte[] Content { get; set; }
    }
}
