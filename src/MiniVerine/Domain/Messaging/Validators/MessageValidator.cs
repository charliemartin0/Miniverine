using FluentValidation;
using MiniVerine.Domain.Messaging.ValueObjects;

namespace MiniVerine.Domain.Messaging.Validators;

public sealed class MessageValidator : AbstractValidator<Message>
{
    public MessageValidator()
    {
        RuleFor(x => x.Value)
            .NotNull()
            .WithMessage("Message body must not be null.");
    }
}
