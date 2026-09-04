namespace MiniVerine.Application.Execution;

/// <summary>
/// Named wrap of a handler throw. Inner exception is what Handle threw.
/// </summary>
public sealed class HandlerFault : Exception
{
    public HandlerFault(Exception innerException)
        : base("The handler failed.", innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
    }
}
