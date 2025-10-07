using Azure;
using IntegratoR.Abstractions.Common.Result;
using IntegratoR.RELion.Domain.DTOs;
using IntegratoR.RELion.Domain.Models;
using IntegratoR.RELion.Interfaces.Services;
using IntegratoR.SampleFunction.Domain.DTOs;
using IntegratoR.SampleFunction.Domain.DTOs.Orchestrators;
using IntegratoR.SampleFunction.Orchestrators;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;

namespace IntegratoR.SampleFunction.Endpoints
{
    /// <summary>
    /// Contains all triggers for journal file processing.
    /// </summary>
    public class JournalTriggers
    {
        private readonly ILogger<JournalTriggers> _logger;
        private readonly IMediator _mediator;
        private readonly IRelionService _relionService;

        private const string TEST_BUSINESS_EVENTID = "BusinessEventsTestEndpointContract";
        private const string RELION_BUSINESS_EVENTID = "INWRelionImportBusinessEvent";

        public JournalTriggers(ILogger<JournalTriggers> logger, IMediator mediator, IRelionService relionService)
        {
            _logger = logger;
            _mediator = mediator;
            _relionService = relionService;
        }

        /// <summary>
        /// Blob Trigger function that starts the journal file processing orchestration
        /// </summary>
        /// <param name="content">File Content</param>
        /// <param name="name">File Name</param>
        /// <param name="client">Instance</param>
        /// <returns></returns>
        [Function("QueueJournalFileProcessing_Trigger")]
        public async Task QueueJournalFileProcessing(
            [BlobTrigger("input/{name}", Connection = "AzureWebJobsStorage")] byte[] content,
            string name,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("New file detected: {Name}. Starting orchestration.", name);

            var orcehstrationInput = new BlobOrchestratorInput { BlobName = name, Content = content };

            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(JournalOrchestrators.ProcessJournalFileOrchestrator), orcehstrationInput);

            _logger.LogInformation("Orchestration for file {Name} started with ID: {InstanceId}", name, instanceId);
        }

        [Function("QueueJournalProcessing_HTTPTrigger")]
        public async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var requestBody = await req.ReadAsStringAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);

            if (string.IsNullOrEmpty(requestBody))
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);

                var error = new Error("QueueJournalProcessing.NoRequestBody",
                    "There was no request body provided for the http trigger",
                    ErrorType.Validation);
                _logger.LogError("Validation error in QueueJournalProcessing_HTTPTrigger: {Error}", JsonConvert.SerializeObject(error));
                await response.WriteStringAsync(error.ToString());

                return response;
            }

            var businessEvent = JsonConvert.DeserializeObject<HTTPOrchestratorInput>(requestBody);

            if (businessEvent == null)
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);

                var error = new Error("QueueJournalProcessing.InvalidBody",
                    "Request Body could not be parsed",
                    ErrorType.Validation);
                _logger.LogError("Validation error in QueueJournalProcessing_HTTPTrigger: {Error}", JsonConvert.SerializeObject(error));

                await response.WriteStringAsync(error.ToString());
                return response;
            }

            // Return ok for test business event id without processing
            if (businessEvent.BusinessEventId == TEST_BUSINESS_EVENTID)
            {
                return response;
            }

            // Make sure its a relion business event
            if (businessEvent.BusinessEventId != RELION_BUSINESS_EVENTID)
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);

                var error = new Error("QueueJournalProcessing.WrongBusinessEvent",
                    "Business event is unknown or not supported by this function",
                    ErrorType.Validation);
                await response.WriteStringAsync(error.ToString());
                return response;
            }

            _logger.LogInformation("Processing INWRelionImportBusinessEvent... Validation of payload successful");

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(JournalOrchestrators.ProcessJournalOrchestrator), businessEvent);

            _logger.LogInformation("Successfully started orchestration with ID = '{InstanceId}'.", instanceId);

            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
