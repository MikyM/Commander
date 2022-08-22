using Autofac;
using Microsoft.Extensions.Options;
using MikyM.Autofac.Extensions;

namespace ResultCommander;

/// <summary>
/// Command handler options.
/// </summary>
[PublicAPI]
public sealed class CommanderConfiguration : IOptions<CommanderConfiguration>
{
    internal CommanderConfiguration(ContainerBuilder builder)
    {
        Builder = builder;
    }

    internal ContainerBuilder Builder { get; set; }

    /// <summary>
    /// Gets or sets the default lifetime of command handlers.
    /// </summary>
    public Lifetime DefaultHandlerLifetime { get; set; } = Lifetime.InstancePerLifetimeScope;
    /// <summary>
    /// Gets or sets the default lifetime of <see cref="ICommandHandlerFactory"/>.
    /// </summary>
    public Lifetime DefaultHandlerFactoryLifetime { get; set; } = Lifetime.InstancePerLifetimeScope;

    /// <inheritdoc/>
    public CommanderConfiguration Value => this;
}
