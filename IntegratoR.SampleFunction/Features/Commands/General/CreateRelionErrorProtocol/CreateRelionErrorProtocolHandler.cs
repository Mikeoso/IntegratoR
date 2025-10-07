using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.SampleFunction.Domain.Entities.Relion;
using IntegratoR.SampleFunction.Features.Commands.General.CreateRelionErrorEntry;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.SampleFunction.Features.Commands.General.CreateRelionErrorProtocol
{
    public class CreateRelionErrorProtocolHandler : IRequestHandler<CreateRelionErrorProcotolCommand, Result<bool>>
    {
        private readonly ILogger<CreateRelionErrorProtocolHandler> _logger;
        private readonly IService<RelionErrorProtocol, string> _service;

        public CreateRelionErrorProtocolHandler(ILogger<CreateRelionErrorProtocolHandler> logger, IService<RelionErrorProtocol, string> service)
        {
            _logger = logger;
            _service = service;
        }

        public async Task<Result<bool>> Handle(CreateRelionErrorProcotolCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating RelionErrorProtocol entry for CorrelationId: {CorrelationId}", request.Id);

            var relionErrorProtocol = new RelionErrorProtocol
            {
                DataAreaId = request.DataAreaId,
                ErrorNum = request.Id,
                ErrorDescription = request.Description,
                ErrorPayload = request.Payload,
            };

            var result = await _service.AddAsync(relionErrorProtocol, cancellationToken);

            if (result.IsFailure)
            {
                var error = result.Error;

                return Result<bool>.Fail(error!);
            }

            _logger.LogInformation("Successfully created RelionErrorProtocol entry with Id: {Id}", request.Id);
            return result.IsSuccess;
        }
    }
}
