using MikyM.Utilities.Results;

namespace ResultCommander;

/// <summary>
/// Defines a base asynchronous command handler. <b>Shouldn't be implemented manually, implement a generic handler interface type instead.</b>
/// </summary>
[PublicAPI]
public interface IAsyncCommandHandlerBase : ICommandHandlerBase
{
}

/// <summary>
/// Defines an asynchronous command handler without a concrete result.
/// </summary>
/// <typeparam name="TCommand">Command type implementing <see cref="ICommand"/>.</typeparam>
[PublicAPI]
public interface IAsyncCommandHandler<in TCommand> : IAsyncCommandHandlerBase where TCommand : class, ICommand
{
    /// <summary>
    /// Handles the given command.
    /// </summary>
    /// <param name="command">Command to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines an asynchronous command handler with a concrete result.
/// </summary>
/// <typeparam name="TCommand">Command type implementing <see cref="ICommand{TResult}"/>,</typeparam>
/// <typeparam name="TResult">Result of the <see cref="ICommand{TResult}"/>,</typeparam>
[PublicAPI]
public interface IAsyncCommandHandler<in TCommand, TResult> : IAsyncCommandHandlerBase where TCommand : class, ICommand<TResult>
{
    /// <summary>
    /// Handles the given command.
    /// </summary>
    /// <param name="command">Command to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="Result"/> of the operation containing a <typeparamref name="TResult"/> if any.</returns>
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
