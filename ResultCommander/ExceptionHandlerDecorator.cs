using Microsoft.Extensions.Logging;
using Remora.Results;

namespace ResultCommander;

/// <summary>
/// Simple exception handling decorator.
/// </summary>
/// <typeparam name="TCommand">Command type.</typeparam>
[PublicAPI]
public class ExceptionAsyncHandlerDecorator<TCommand> : IAsyncCommandHandler<TCommand> where TCommand : class, ICommand
{
    private readonly IAsyncCommandHandler<TCommand> _handler;
    private readonly ILogger<ExceptionAsyncHandlerDecorator<TCommand>> _logger;

    /// <summary>
    /// Constructor.
    /// </summary>
    public ExceptionAsyncHandlerDecorator(IAsyncCommandHandler<TCommand> handler, ILogger<ExceptionAsyncHandlerDecorator<TCommand>> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        Result result;

        try
        {
            result = await _handler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while handling a command");
            return ex;
        }

        return result;
    }
}

/// <summary>
/// Simple exception handling decorator.
/// </summary>
/// <typeparam name="TCommand">Command type.</typeparam>
/// <typeparam name="TResult">Result type.</typeparam>
[PublicAPI]
public class ExceptionAsyncHandlerDecorator<TCommand, TResult> : IAsyncCommandHandler<TCommand, TResult> where TCommand : class, ICommand<TResult>
{
    private readonly IAsyncCommandHandler<TCommand, TResult> _handler;
    private readonly ILogger<ExceptionAsyncHandlerDecorator<TCommand, TResult>> _logger;

    /// <summary>
    /// Constructor.
    /// </summary>
    public ExceptionAsyncHandlerDecorator(IAsyncCommandHandler<TCommand, TResult> handler, ILogger<ExceptionAsyncHandlerDecorator<TCommand, TResult>> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        Result<TResult> result;

        try
        {
            result = await _handler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while handling a command");
            return ex;
        }

        return result;
    }
}

/// <summary>
/// Simple exception handling decorator.
/// </summary>
/// <typeparam name="TCommand">Command type.</typeparam>
[PublicAPI]
public class ExceptionSyncHandlerDecorator<TCommand> : ISyncCommandHandler<TCommand> where TCommand : class, ICommand
{
    private readonly ISyncCommandHandler<TCommand> _handler;
    private readonly ILogger<ExceptionSyncHandlerDecorator<TCommand>> _logger;

    /// <summary>
    /// Constructor.
    /// </summary>
    public ExceptionSyncHandlerDecorator(ISyncCommandHandler<TCommand> handler, ILogger<ExceptionSyncHandlerDecorator<TCommand>> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <inheritdoc />
    public Result Handle(TCommand command)
    {
        Result result;

        try
        {
            result = _handler.Handle(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while handling a command");
            return ex;
        }

        return result;
    }
}

/// <summary>
/// Simple exception handling decorator.
/// </summary>
/// <typeparam name="TCommand">Command type.</typeparam>
/// <typeparam name="TResult">Result type.</typeparam>
[PublicAPI]
public class ExceptionSyncHandlerDecorator<TCommand, TResult> : ISyncCommandHandler<TCommand, TResult> where TCommand : class, ICommand<TResult>
{
    private readonly ISyncCommandHandler<TCommand, TResult> _handler;
    private readonly ILogger<ExceptionSyncHandlerDecorator<TCommand, TResult>> _logger;

    /// <summary>
    /// Constructor.
    /// </summary>
    public ExceptionSyncHandlerDecorator(ISyncCommandHandler<TCommand, TResult> handler, ILogger<ExceptionSyncHandlerDecorator<TCommand, TResult>> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <inheritdoc />
    public Result<TResult> Handle(TCommand command)
    {
        Result<TResult> result;

        try
        {
            result = _handler.Handle(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while handling a command");
            return ex;
        }

        return result;
    }
}
