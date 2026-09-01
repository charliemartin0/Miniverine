using FluentValidation;
using MiniVerine.Domain.Messaging.ValueObjects;

namespace MiniVerine.Domain.Messaging.Validators;

public sealed class MessageTypeValidator : AbstractValidator<MessageType>
{
    public MessageTypeValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("MessageType must not be empty.")
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("MessageType must not be whitespace.");
    }
}
