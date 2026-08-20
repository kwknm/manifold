using Catalog.Api.Contracts;
using FluentValidation;

namespace Catalog.Api.Validators;

public class AddBookRequestValidator : AbstractValidator<AddBookRequest>
{
    public AddBookRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");
            
        RuleFor(x => x.Author)
            .NotEmpty().WithMessage("Author is required")
            .MaximumLength(100).WithMessage("Author must not exceed 100 characters");
            
        RuleFor(x => x.Isbn)
            .NotEmpty().WithMessage("ISBN is required")
            .MaximumLength(20).WithMessage("ISBN must not exceed 20 characters")
            .Matches(@"^[0-9-]*$").WithMessage("ISBN must contain only numbers and hyphens");
            
        RuleFor(x => x.File)
            .NotNull().WithMessage("File is required")
            .Must(file => file.Length > 0).WithMessage("File cannot be empty")
            .Must(file => file.Length < 10 * 1024 * 1024).WithMessage("File size must be less than 10MB")
            .Must(file => {
                var allowedTypes = new[] { "application/pdf", "application/epub+zip" };
                return allowedTypes.Contains(file.ContentType);
            }).WithMessage("File must be PDF or EPUB");
    }

}