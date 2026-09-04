using MiniVerine.Domain.Envelope;

namespace MiniVerine.Application.Execution;

/// <summary>
/// Port for envelopes whose Invoke retries are exhausted. Persistence implements the durable table later.
/// </summary>
public interface IErrorQueue
{
    void Move(Envelope envelope);
}
