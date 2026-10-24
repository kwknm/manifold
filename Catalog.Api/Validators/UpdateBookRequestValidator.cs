using Catalog.Api.Contracts;
using FluentValidation;

namespace Catalog.Api.Validators;

public class UpdateBookRequestValidator : AbstractValidator<UpdateBookRequest>
{
    public UpdateBookRequestValidator()
    {
        When(x => x.Title is not null, () =>
        {
            RuleFor(x => x.Title!)
                .NotEmpty().WithMessage("Title cannot be empty")
                .MaximumLength(200).WithMessage("Title must not exceed 200 characters");
        });

        When(x => x.Isbn is not null, () =>
        {
            RuleFor(x => x.Isbn!)
                .MaximumLength(20).WithMessage("ISBN must not exceed 20 characters");
        });

        When(x => x.Authors is not null, () =>
        {
            RuleForEach(x => x.Authors!)
                .NotEmpty().WithMessage("Author name cannot be empty")
                .MaximumLength(64).WithMessage("Author name must not exceed 64 characters");
        });
    }
}
