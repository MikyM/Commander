namespace ResultCommander;

/// <summary>
/// Marks a handler to be skipped from automatic registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[PublicAPI]
public class SkipHandlerRegistrationAttribute : Attribute
{
}
