using System.Reflection;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.FO.Domain.Models.Settings;

namespace IntegratoR.Hosting;

/// <summary>
/// Builder for configuring IntegratoR framework services during DI registration.
/// </summary>
public sealed class IntegratoRBuilder
{
    internal List<Assembly> ConsumerAssemblies { get; } = [];
    internal Action<ODataSettings>? ODataPostConfigure { get; private set; }
    internal Action<FOSettings>? FOPostConfigure { get; private set; }

    /// <summary>
    /// Registers MediatR handlers and FluentValidation validators from the specified consumer assemblies.
    /// </summary>
    public IntegratoRBuilder AddConsumerHandlers(params Assembly[] assemblies)
    {
        ConsumerAssemblies.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Applies programmatic overrides to the OData connection settings after configuration binding.
    /// Multiple calls are composed; all delegates run in registration order.
    /// </summary>
    public IntegratoRBuilder ConfigureOData(Action<ODataSettings> configure)
    {
        ODataPostConfigure = ODataPostConfigure is null
            ? configure
            : ODataPostConfigure + configure;
        return this;
    }

    /// <summary>
    /// Applies programmatic overrides to the F&amp;O settings after configuration binding.
    /// Multiple calls are composed; all delegates run in registration order.
    /// </summary>
    public IntegratoRBuilder ConfigureFO(Action<FOSettings> configure)
    {
        FOPostConfigure = FOPostConfigure is null
            ? configure
            : FOPostConfigure + configure;
        return this;
    }
}
