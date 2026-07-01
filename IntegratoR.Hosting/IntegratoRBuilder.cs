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
    /// Registers the specified consumer assemblies' FluentValidation validators and folds them into
    /// <c>AddIntegratoR</c>'s combined <c>RegisterGenericHandlers</c> MediatR scan, so the framework's
    /// generic CRUD/query handlers close over the entity types declared in those assemblies — including
    /// subclasses of framework entities.
    /// </summary>
    /// <param name="assemblies">The consumer assemblies to scan for handlers and validators.</param>
    /// <returns>The same builder instance.</returns>
    public IntegratoRBuilder AddConsumerHandlers(params Assembly[] assemblies)
    {
        ConsumerAssemblies.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Applies programmatic overrides to the OData connection settings after configuration binding.
    /// </summary>
    /// <param name="configure">A delegate that overrides the bound <see cref="ODataSettings"/>.</param>
    /// <returns>The same builder instance.</returns>
    /// <remarks>Multiple calls are composed; all delegates run in registration order.</remarks>
    public IntegratoRBuilder ConfigureOData(Action<ODataSettings> configure)
    {
        ODataPostConfigure = ODataPostConfigure is null
            ? configure
            : ODataPostConfigure + configure;
        return this;
    }

    /// <summary>
    /// Applies programmatic overrides to the F&amp;O settings after configuration binding.
    /// </summary>
    /// <param name="configure">A delegate that overrides the bound <see cref="FOSettings"/>.</param>
    /// <returns>The same builder instance.</returns>
    /// <remarks>Multiple calls are composed; all delegates run in registration order.</remarks>
    public IntegratoRBuilder ConfigureFO(Action<FOSettings> configure)
    {
        FOPostConfigure = FOPostConfigure is null
            ? configure
            : FOPostConfigure + configure;
        return this;
    }
}
