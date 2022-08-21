using System.Text.Json;

namespace Commander;

/// <summary>
/// Defines a base command. <b>Used ONLY as a marker interface.</b>
/// </summary>
/// <remarks>Overrides <see cref="ToString"/> to return a Json representation.</remarks>
[PublicAPI]
public abstract class Command : ICommand
{
    /// <summary>
    /// Serializes this to json using <see cref="JsonSerializer"/>.
    /// </summary>
    public override string ToString()
        => JsonSerializer.Serialize(this);
}

/// <summary>
/// Defines a base command with a concrete result.
/// </summary>
/// <typeparam name="TResult">The type of the result of this command.</typeparam>
/// <remarks>Overrides <see cref="ToString"/> to return a Json representation.</remarks>
[PublicAPI]
public abstract class Command<TResult> : ICommand<TResult>
{
    /// <summary>
    /// Serializes this to json using <see cref="JsonSerializer"/>.
    /// </summary>
    public override string ToString()
        => JsonSerializer.Serialize(this);
}
