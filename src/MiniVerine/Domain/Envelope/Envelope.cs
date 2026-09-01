using MiniVerine.Domain.Envelope.ValueObjects;
using MiniVerine.Domain.Messaging.ValueObjects;
using MiniVerine.Domain.Sagas.ValueObjects;

namespace MiniVerine.Domain.Envelope;

public record Envelope(
    EnvelopeId Id,
    Message Message,
    MessageType MessageType,
    Destination Destination,
    CorrelationId CorrelationId,
    ConversationId ConversationId,
    SagaId SagaId,
    SentAt SentAt,
    DeliverBy DeliverBy,
    Headers Headers,
    ContentType ContentType,
    Attempts Attempts,
    EnvelopeData Data);
