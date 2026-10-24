using ErrorOr;

namespace Catalog.Api.Errors;

public static class CatalogErrors
{
    public static class Book
    {
        public static Error FailedToCreate => Error.Failure(
            code: "Book.FailedToCreate",
            description: "Failed to create book.");

        public static Error NotFound => Error.NotFound(
            code: "Book.NotFound",
            description: "Book not found.");
    }
    
    public static class Tag
    {
        public static Error FailedToCreate => Error.Failure(
            code: "Tag.FailedToCreate",
            description: "Failed to create tag.");
    }
}