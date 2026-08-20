using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace Shared.Extensions;

public static class ResultExtensions
{
    public static IResult MapToResponseAsync<TValue>(this ErrorOr<TValue> result,
        Func<TValue, IResult> onSuccess)
    {
        return result.Match(onSuccess, ToProblem);
    }

    private static IResult ToProblem(List<Error> errors)
    {
        var firstError = errors.FirstOrDefault();

        var statusCode = firstError.Type switch
        {
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,

            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            detail: firstError.Description,
            statusCode: statusCode);
    }
}