using Catalog.Api.Contracts;
using FluentValidation;
using Shared;

namespace Catalog.Api.Validators;

public class AddBookRequestValidator : AbstractValidator<AddBookRequest>
{
    private readonly string[] _allowedTypes =
    [
        ContentType.PDF.ToValue(), ContentType.EPUB.ToValue(), ContentType.FB2.ToValue()
    ];

    private const int MaxFileSizeInMb = 10;

    public AddBookRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Author)
            .MaximumLength(100).WithMessage("Author must not exceed 100 characters");

        RuleFor(x => x.Isbn)
            .MaximumLength(20).WithMessage("ISBN must not exceed 20 characters")
            .Matches(@"^[0-9-]*$").WithMessage("ISBN must contain only numbers and hyphens");

        RuleFor(x => x.File)
            .NotNull().WithMessage("File is required");

        RuleFor(x => x.TagIds)
            .Must(tagIds => tagIds is null || tagIds.Count <= 10)
            .WithMessage("Cannot have more than 10 tags");

        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File!)
                .Must(file => file.Length > 0).WithMessage("File cannot be empty")
                .Must(file => file.Length < MaxFileSizeInMb * 1024 * 1024)
                .WithMessage($"File size must be less than {MaxFileSizeInMb}MB")
                .Must(file => _allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                .WithMessage("File must be PDF, EPUB, or FB2");
        });
    }
}