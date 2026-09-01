using FluentValidation;
using MiniVerine.Domain.Envelope.ValueObjects;

namespace MiniVerine.Domain.Envelope.Validators;

public sealed class EnvelopeDataValidator : AbstractValidator<EnvelopeData>
{
    public EnvelopeDataValidator()
    {
        // Empty is valid until Serialization has run. Pairing with ContentType is on EnvelopeValidator.
    }
}
