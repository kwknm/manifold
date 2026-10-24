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

    public AddBookRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required");

        RuleFor(x => x.File)
            .Must(file => file.Length > 0)
                .WithMessage("File cannot be empty")
            .Must(file => file.Length <= UploadLimits.MaxBookFileSizeBytes)
                .WithMessage("File size must not exceed 100MB")
            .Must(file => _allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                .WithMessage("File must be PDF, EPUB, or FB2")
            .When(x => x.File is not null);

        RuleFor(x => x.TagIds)
            .Must(BeValidGuidList)
                .WithMessage("TagIds must be a comma-separated list of GUIDs")
            .When(x => !string.IsNullOrWhiteSpace(x.TagIds));
    }

    private static bool BeValidGuidList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(part => Guid.TryParse(part, out _));
    }
}
