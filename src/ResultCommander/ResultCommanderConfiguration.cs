using ServiceLifetime = AttributeBasedRegistration.ServiceLifetime;

namespace ResultCommander;

/// <summary>
/// Command handler options.
/// </summary>
[PublicAPI]
public sealed class ResultCommanderConfiguration
{
    /// <summary>
    /// Gets or sets the default lifetime of command handlers.
    /// </summary>
    public ServiceLifetime DefaultHandlerLifetime { get; set; } = ServiceLifetime.InstancePerLifetimeScope;
    
    /// <summary>
    /// Gets or sets the default lifetime of <see cref="ICommandHandlerResolver"/>.
    /// </summary>
    public ServiceLifetime DefaultHandlerResolverLifetime { get; set; } = ServiceLifetime.InstancePerLifetimeScope;
}
