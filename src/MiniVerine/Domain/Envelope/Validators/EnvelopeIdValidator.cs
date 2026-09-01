using FluentValidation;
using MiniVerine.Domain.Envelope.ValueObjects;

namespace MiniVerine.Domain.Envelope.Validators;

public sealed class EnvelopeIdValidator : AbstractValidator<EnvelopeId>
{
    public EnvelopeIdValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("EnvelopeId must not be an empty Guid.");
    }
}
