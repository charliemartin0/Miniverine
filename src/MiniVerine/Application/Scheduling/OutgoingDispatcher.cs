using MiniVerine.Application.Bus;
using MiniVerine.Application.Cascades;
using MiniVerine.Application.Execution;
using MiniVerine.Domain.Envelope;
using MiniVerine.Domain.Envelope.Validators;
using MiniVerine.Domain.Envelope.ValueObjects;
using MiniVerine.Domain.Messaging;
using MiniVerine.Domain.Messaging.ValueObjects;
using MiniVerine.Domain.Sagas.ValueObjects;

namespace MiniVerine.Application.Scheduling;

/// <summary>
/// After handler success (and delayed Publish): park delayed envelopes, publish immediate bodies.
/// </summary>
public sealed class OutgoingDispatcher
{
    private static readonly EnvelopeValidator Validator = new();
    private static readonly Uri ScheduledDestination = new("local://scheduled/");

    private readonly ICascadePublisher? _immediate;
    private readonly IScheduledEnvelopeHold _hold;

    public OutgoingDispatcher(IScheduledEnvelopeHold hold, ICascadePublisher? immediate = null)
    {
        ArgumentNullException.ThrowIfNull(hold);
        _hold = hold;
        _immediate = immediate;
    }

    public bool TryPark(object message, DeliveryOptions? options, Envelope? parent = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        DateTimeOffset sentAt = DateTimeOffset.UtcNow;
        DateTimeOffset? due = DelayedDelivery.DueAt(message.GetType(), options, sentAt);
        if (due is null)
        {
            return false;
        }

        _hold.Park(Build(message, sentAt, due.Value, parent));
        return true;
    }

    public void Dispatch(IReadOnlyList<object> outgoing, Envelope parent)
    {
        ArgumentNullException.ThrowIfNull(outgoing);
        ArgumentNullException.ThrowIfNull(parent);
        var immediate = new List<object>();
        foreach (object message in outgoing)
        {
            if (!TryPark(message, options: null, parent))
            {
                immediate.Add(message);
            }
        }

        if (immediate.Count > 0)
        {
            _immediate?.Publish(immediate);
        }
    }

    private static Envelope Build(object message, DateTimeOffset sentAt, DateTimeOffset due, Envelope? parent)
    {
        Envelope envelope = new(
            new EnvelopeId(Guid.NewGuid()),
            new Message(message),
            MessageTypeNaming.For(message.GetType()),
            new Destination(ScheduledDestination),
            parent?.CorrelationId ?? new CorrelationId(Guid.NewGuid()),
            parent?.ConversationId ?? new ConversationId(Guid.NewGuid()),
            parent?.SagaId ?? new SagaId(""),
            new SentAt(sentAt),
            new DeliverBy(due),
            new Headers(),
            new ContentType(""),
            new Attempts(1),
            new EnvelopeData());

        var result = Validator.Validate(envelope);
        if (!result.IsValid)
        {
            throw new FluentValidation.ValidationException(result.Errors);
        }

        return envelope;
    }
}
