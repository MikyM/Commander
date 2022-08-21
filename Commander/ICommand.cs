namespace Commander;

/// <summary>
/// Defines a base command. <b>Shouldn't be implemented manually, implement <see cref="ICommand"/> or <see cref="ICommand{TResult}"/> instead.</b>
/// </summary>
public interface ICommandBase
{
}

/// <summary>
/// Defines a base command without a concrete result.
/// </summary>
[PublicAPI]
public interface ICommand : ICommandBase
{
}

/// <summary>
/// Defines a base command with a concrete result.
/// </summary>
[PublicAPI]
public interface ICommand<TResult> : ICommandBase
{
}
