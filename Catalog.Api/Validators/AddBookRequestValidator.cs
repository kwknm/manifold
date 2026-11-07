using Catalog.Api.Contracts;
using FluentValidation;
using Shared;

namespace Catalog.Api.Validators;

public class AddBookRequestValidator : AbstractValidator<AddBookRequest>
{
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
            // Content type and extension must agree: the metadata service selects the parser
            // by extension, so a mismatch would run the wrong parser on the file.
            .Must(file => BookFormats.Resolve(file.FileName, file.ContentType) is not null)
                .WithMessage($"File must be one of: {BookFormats.DescribeAllowed()}")
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
