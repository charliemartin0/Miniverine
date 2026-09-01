using MiniVerine.Domain.Envelope;
using MiniVerine.Domain.Envelope.ValueObjects;
using MiniVerine.Domain.Messaging;
using MiniVerine.Domain.Messaging.ValueObjects;
using MiniVerine.Domain.Sagas.ValueObjects;

namespace MiniVerine.Tests.Domain.Envelope;

internal static class EnvelopeFactory
{
    public static MiniVerine.Domain.Envelope.Envelope Create(
        EnvelopeId? id = null,
        Message? message = null,
        MessageType? messageType = null,
        Destination? destination = null,
        CorrelationId? correlationId = null,
        ConversationId? conversationId = null,
        SagaId? sagaId = null,
        DateTimeOffset? sentAt = null,
        DateTimeOffset? deliverBy = null,
        Headers? headers = null,
        string contentType = "",
        int attempts = 1,
        byte[]? data = null)
    {
        var sent = sentAt ?? DateTimeOffset.UtcNow;
        return new MiniVerine.Domain.Envelope.Envelope(
            id ?? new EnvelopeId(Guid.NewGuid()),
            message ?? new Message(new PlaceOrder(1)),
            messageType ?? MessageTypeNaming.For(typeof(PlaceOrder)),
            destination ?? new Destination(new Uri("local://payments/")),
            correlationId ?? new CorrelationId(Guid.NewGuid()),
            conversationId ?? new ConversationId(Guid.NewGuid()),
            sagaId ?? new SagaId(""),
            new SentAt(sent),
            new DeliverBy(deliverBy),
            headers ?? new Headers(),
            new ContentType(contentType),
            new Attempts(attempts),
            data is null ? new EnvelopeData() : new EnvelopeData(data));
    }
}
