using AttributeBasedRegistration.Attributes.Abstractions;

namespace ResultCommander;

/// <summary>
/// Marks a handler to be skipped from automatic registration.
/// </summary>
[PublicAPI]
public interface ISkipHandlerRegistrationAttribute : ISkipRegistrationAttribute
{
}

/// <summary>
/// Marks a handler to be skipped from automatic registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[PublicAPI]
public sealed class SkipHandlerRegistrationAttribute : Attribute, ISkipHandlerRegistrationAttribute
{
}
