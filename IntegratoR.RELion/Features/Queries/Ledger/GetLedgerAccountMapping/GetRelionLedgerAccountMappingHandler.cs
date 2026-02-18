using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentResults;
using IntegratoR.RELion.Domain.Models;
using IntegratoR.RELion.Features.Queries.Ledger.GetLedgerAccountMapping;
using IntegratoR.RELion.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.RELion.Features.Queries.Ledger.GetLedgerAccountMapping
{
    public class GetRelionLedgerAccountMappingHandler : IRequestHandler<GetRelionLedgerAccountMappingQuery, Result<RelionLedgerAccountMapping>>
    {
        private readonly ILogger<GetRelionLedgerAccountMappingHandler> _logger;
        private readonly IRelionService _relionService;

        public GetRelionLedgerAccountMappingHandler(ILogger<GetRelionLedgerAccountMappingHandler> logger, IRelionService relionService)
        {
            _logger = logger;
            _relionService = relionService;
        }

        public async Task<Result<RelionLedgerAccountMapping>> Handle(GetRelionLedgerAccountMappingQuery request, CancellationToken cancellationToken)
        {
            var result = await _relionService.GetLedgerAccountMappingsAsync(request.EntryNo, cancellationToken).ConfigureAwait(false);

            return result;
        }
    }
}
