using ErrorOr;

namespace Catalog.Api.Errors;

public static class CatalogErrors
{
    public static class Book
    {
        public static Error FailedToCreate => Error.Failure(
            code: "Book.FailedToCreate",
            description: "Failed to create book.");

        public static Error FailedToUpdate => Error.Failure(
            code: "Book.FailedToUpdate",
            description: "Failed to update book.");

        public static Error FailedToDelete => Error.Failure(
            code: "Book.FailedToDelete",
            description: "Failed to delete book.");

        public static Error NotFound => Error.NotFound(
            code: "Book.NotFound",
            description: "Book not found.");

        public static Error InvalidTags => Error.Validation(
            code: "Book.InvalidTags",
            description: "One or more tags were not found.");
    }

    public static class Tag
    {
        public static Error FailedToCreate => Error.Failure(
            code: "Tag.FailedToCreate",
            description: "Failed to create tag.");

        public static Error FailedToUpdate => Error.Failure(
            code: "Tag.FailedToUpdate",
            description: "Failed to update tag.");

        public static Error FailedToDelete => Error.Failure(
            code: "Tag.FailedToDelete",
            description: "Failed to delete tag.");

        public static Error NotFound => Error.NotFound(
            code: "Tag.NotFound",
            description: "Tag not found.");

        public static Error AlreadyExists => Error.Conflict(
            code: "Tag.AlreadyExists",
            description: "Tag with the same name already exists.");
    }
}
