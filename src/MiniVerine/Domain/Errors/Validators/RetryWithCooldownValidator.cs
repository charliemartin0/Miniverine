using FluentValidation;
using MiniVerine.Domain.Errors.ValueObjects;

namespace MiniVerine.Domain.Errors.Validators;

public sealed class RetryWithCooldownValidator : AbstractValidator<RetryWithCooldown>
{
    public RetryWithCooldownValidator()
    {
        RuleFor(x => x.Delay)
            .GreaterThanOrEqualTo(TimeSpan.Zero)
            .WithMessage("RetryWithCooldown delay must not be negative.");
    }
}
