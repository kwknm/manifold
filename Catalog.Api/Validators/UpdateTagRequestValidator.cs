using Catalog.Api.Contracts;
using FluentValidation;

namespace Catalog.Api.Validators;

public class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.ColorHex)
            .NotEmpty().WithMessage("Color is required")
            .Matches(@"^#[0-9a-fA-F]{6}$").WithMessage("Invalid color format");
    }
}
