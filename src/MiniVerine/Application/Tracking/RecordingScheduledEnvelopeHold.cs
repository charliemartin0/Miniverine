using MiniVerine.Application.Execution;
using MiniVerine.Domain.Envelope;
using MiniVerine.Domain.Envelope.ValueObjects;

namespace MiniVerine.Application.Tracking;

internal sealed class RecordingScheduledEnvelopeHold : IScheduledEnvelopeHold
{
    private readonly IScheduledEnvelopeHold _inner;
    private readonly Action<Envelope> _onPark;

    public RecordingScheduledEnvelopeHold(IScheduledEnvelopeHold inner, Action<Envelope> onPark)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(onPark);
        _inner = inner;
        _onPark = onPark;
    }

    public IReadOnlyList<Envelope> Peek() => _inner.Peek();

    public void Park(Envelope envelope)
    {
        _onPark(envelope);
        _inner.Park(envelope);
    }

    public bool TryRemove(EnvelopeId id) => _inner.TryRemove(id);

    public IDisposable BeginPlay() => _inner.BeginPlay();
}
