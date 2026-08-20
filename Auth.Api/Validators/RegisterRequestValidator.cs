using Auth.Api.Contracts;
using FluentValidation;

namespace Auth.Api.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        RuleFor(x => x.UserName)
            .NotEmpty()
            .MaximumLength(12);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(6);
    }
}
