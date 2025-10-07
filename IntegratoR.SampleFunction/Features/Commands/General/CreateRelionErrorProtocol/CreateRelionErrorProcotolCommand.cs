using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Commands;

namespace IntegratoR.SampleFunction.Features.Commands.General.CreateRelionErrorProtocol
{
    public record CreateRelionErrorProcotolCommand(string DataAreaId, string Id, string Description, string Payload) : ICommand<Result<bool>>;
}
