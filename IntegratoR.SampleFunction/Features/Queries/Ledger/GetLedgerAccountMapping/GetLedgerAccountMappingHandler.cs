using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.SampleFunction.Domain.Entities.Ledger;
using IntegratoR.SampleFunction.Features.Queries.Ledger.GetLedgerAccountMapping;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegratoR.SampleFunction.Features.Queries.Ledger.GetLedgerAccountMapping
{
    public class GetLedgerAccountMappingHandler : IRequestHandler<GetLedgerAccountMappingQuery, Result<LedgerAccountMapping>>
    {
        private readonly ILogger<GetLedgerAccountMappingHandler> _logger;
        private readonly IService<LedgerAccountMapping, string> _ledgerAccountMappingService;

        public GetLedgerAccountMappingHandler(ILogger<GetLedgerAccountMappingHandler> logger, IService<LedgerAccountMapping, string> ledgerAccountMappingService)
        {
            _logger = logger;
            _ledgerAccountMappingService = ledgerAccountMappingService;
        }

        public async Task<Result<LedgerAccountMapping>> Handle(GetLedgerAccountMappingQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Querying LedgerAccountMapping for RelionLedgerAccount: {RelionLedgerAccount}, RelionLedgerIFRS: {RelionLedgerIFRS}",
                request.LedgerAccount,
                request.IFRS);

            Result<IEnumerable<LedgerAccountMapping>> result;

            if (string.IsNullOrEmpty(request.IFRS))
            {
                result = await _ledgerAccountMappingService.FindAsync(
                    x => x.RelionLedgerAccount == request.LedgerAccount,
                    cancellationToken);
            }
            else
            {
                result = await _ledgerAccountMappingService.FindAsync(
                    x => x.RelionLedgerAccount == request.LedgerAccount &&
                         x.RelionLedgerIFRS == request.IFRS,
                    cancellationToken);
            }

            if (result.IsFailure)
            {
                return Result<LedgerAccountMapping>.Fail(result);
            }

            var ledgerAccountMapping = result?.Value?.FirstOrDefault();
            return Result<LedgerAccountMapping>.Ok(ledgerAccountMapping!);
        }
    }
}
