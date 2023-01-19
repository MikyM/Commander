using Microsoft.Extensions.DependencyInjection;

namespace ResultCommander;

/// <summary>
/// <para>
///     A resolver for command handlers utilizing injected current -scoped- <see cref="IServiceProvider"/> to resolve optional handlers (like <see cref="Lazy{T}"/>).
/// </para>
/// <para>
///     Use this only when for some reason you don't want to utilize <see cref="Lazy{T}"/> and when dealing with optional dependencies which creation is expensive.
/// </para>
/// </summary>
[PublicAPI]
public interface ICommandHandlerResolver
{
    /// <summary>
    /// Gets a command handler of a given type.
    /// </summary>
    /// <typeparam name="TCommandHandler">Type of the handler to get.</typeparam>
    /// <returns>The searched for handler.</returns>
    TCommandHandler GetHandler<TCommandHandler>() where TCommandHandler : class, ICommandHandlerBase;

    /// <summary>
    /// Gets an <see cref="IAsyncCommandHandler{TCommand,TResult}"/> for a given <see cref="ICommand{TResult}"/>.
    /// </summary>
    /// <typeparam name="TCommand">Type of the <see cref="ICommand{TResult}"/>.</typeparam>
    /// <typeparam name="TResult">Type of the command result.</typeparam>
    /// <returns>The searched for handler.</returns>
    IAsyncCommandHandler<TCommand, TResult> GetAsyncHandlerFor<TCommand, TResult>() where TCommand : class, ICommand<TResult>;

    /// <summary>
    /// Gets an <see cref="IAsyncCommandHandler{TCommand}"/> for a given <see cref="ICommand"/>.
    /// </summary>
    /// <typeparam name="TCommand">Type of the <see cref="ICommand"/>.</typeparam>
    /// <returns>The searched for handler.</returns>
    IAsyncCommandHandler<TCommand> GetAsyncHandlerFor<TCommand>() where TCommand : class, ICommand;
    
    /// <summary>
    /// Gets an <see cref="ISyncCommandHandler{TCommand,TResult}"/> for a given <see cref="ICommand{TResult}"/>.
    /// </summary>
    /// <typeparam name="TCommand">Type of the <see cref="ICommand{TResult}"/>.</typeparam>
    /// <typeparam name="TResult">Type of the command result.</typeparam>
    /// <returns>The searched for handler.</returns>
    ISyncCommandHandler<TCommand, TResult> GetSyncHandlerFor<TCommand, TResult>() where TCommand : class, ICommand<TResult>;

    /// <summary>
    /// Gets an <see cref="ISyncCommandHandler{TCommand}"/> for a given <see cref="ICommand"/>
    /// </summary>
    /// <typeparam name="TCommand">Type of the <see cref="ICommand"/>.</typeparam>
    /// <returns>The searched for handler.</returns>
    ISyncCommandHandler<TCommand> GetSyncHandlerFor<TCommand>() where TCommand : class, ICommand;
}

/// <inheritdoc cref="ICommandHandlerResolver"/>
[PublicAPI]
public class CommandHandlerResolver : ICommandHandlerResolver
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Creates a new instance of <see cref="CommandHandlerResolver"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public CommandHandlerResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public TCommandHandler GetHandler<TCommandHandler>() where TCommandHandler : class, ICommandHandlerBase
    {
        if (!typeof(TCommandHandler).IsInterface)
            throw new ArgumentException("Due to Autofac limitations you must use open generic interfaces");
        
        return _serviceProvider.GetRequiredService<TCommandHandler>();
    }

    /// <inheritdoc />
    public IAsyncCommandHandler<TCommand> GetAsyncHandlerFor<TCommand>() where TCommand : class, ICommand
        => _serviceProvider.GetRequiredService<IAsyncCommandHandler<TCommand>>();

    /// <inheritdoc />
    public IAsyncCommandHandler<TCommand, TResult> GetAsyncHandlerFor<TCommand, TResult>()
        where TCommand : class, ICommand<TResult>
        => _serviceProvider.GetRequiredService<IAsyncCommandHandler<TCommand, TResult>>();
    
    /// <inheritdoc />
    public ISyncCommandHandler<TCommand> GetSyncHandlerFor<TCommand>() where TCommand : class, ICommand
        => _serviceProvider.GetRequiredService<ISyncCommandHandler<TCommand>>();

    /// <inheritdoc />
    public ISyncCommandHandler<TCommand, TResult> GetSyncHandlerFor<TCommand, TResult>()
        where TCommand : class, ICommand<TResult>
        => _serviceProvider.GetRequiredService<ISyncCommandHandler<TCommand,TResult>>();
}
