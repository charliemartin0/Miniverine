namespace MiniVerine.Application.Bus;

/// <summary>
/// Public facade handlers and the sample host talk to. Invoke waits for the handler;
/// Publish accepts and lets go. Does not own threads or sockets.
/// </summary>
public interface IMessageBus
{
    Task InvokeAsync(object message, CancellationToken cancellationToken = default);

    Task<TResult> InvokeAsync<TResult>(object message, CancellationToken cancellationToken = default);

    Task PublishAsync(object message, CancellationToken cancellationToken = default);
}
