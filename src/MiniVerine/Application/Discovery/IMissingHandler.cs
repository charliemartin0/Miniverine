namespace MiniVerine.Application.Discovery;

/// <summary>
/// Port for a message type with no discovered handlers. Execution calls this; Discovery only defines the hook.
/// </summary>
public interface IMissingHandler;
