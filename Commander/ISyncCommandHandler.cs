using MikyM.Common.Utilities.Results;

namespace Commander;

/// <summary>
/// Defines a base synchronous command handler. <b>Shouldn't be implemented manually, implement a generic handler interface type instead.</b>
/// </summary>
[PublicAPI]
public interface ISyncCommandHandlerBase : ICommandHandlerBase
{
}

/// <summary>
/// Defines a synchronous command handler without a concrete result.
/// </summary>
/// <typeparam name="TCommand">Command type implementing <see cref="ICommand"/>.</typeparam>
[PublicAPI]
public interface ISyncCommandHandler<in TCommand> : ISyncCommandHandlerBase where TCommand : class, ICommand
{
    /// <summary>
    /// Handles the given command.
    /// </summary>
    /// <param name="command">Command to handle.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    Result Handle(TCommand command);
}

/// <summary>
/// Defines a synchronous command handler with a concrete result.
/// </summary>
/// <typeparam name="TCommand">Command type implementing <see cref="ICommand{TResult}"/>,</typeparam>
/// <typeparam name="TResult">Result of the <see cref="ICommand{TResult}"/>,</typeparam>
[PublicAPI]
public interface ISyncCommandHandler<in TCommand, TResult> : ISyncCommandHandlerBase where TCommand : class, ICommand<TResult>
{
    /// <summary>
    /// Handles the given command.
    /// </summary>
    /// <param name="command">Command to handle.</param>
    /// <returns>The <see cref="Result"/> of the operation containing a <typeparamref name="TResult"/> if any.</returns>
    Result<TResult> Handle(TCommand command);
}
