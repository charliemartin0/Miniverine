namespace MiniVerine.Infrastructure.Serialization;

/// <summary>
/// Folder: Envelope body ↔ bytes. System.Text.Json, ContentType, headers preserved.
///
/// Put here: serialize/deserialize using Domain/Messaging type names. Polymorphic
/// payloads / type discriminators if you need them. Versioning of contracts later.
///
/// Do not put here: routing, retries, or “what is a message.” Unknown CLR type is a
/// handled failure (handoff to Execution / IMissingHandler), not a crash in the serializer.
///
/// Prove with: object → bytes → object, headers intact, in MiniVerine.Tests.
/// </summary>
public sealed class SerializationPlan;
