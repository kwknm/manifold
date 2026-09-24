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

    private const int MaxFileSizeInMb = 100;

    public AddBookRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required");

        RuleFor(x => x.File)
            .Must(file => file.Length > 0)
                .WithMessage("File cannot be empty")
            .Must(file => file.Length < MaxFileSizeInMb * 1024 * 1024)
                .WithMessage($"File size must be less than {MaxFileSizeInMb}MB")
            .Must(file => _allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                .WithMessage("File must be PDF, EPUB, or FB2")
            .When(x => x.File is not null);
    }
}