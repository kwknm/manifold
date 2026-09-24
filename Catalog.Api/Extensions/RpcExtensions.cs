using Grpc.Core;

namespace Catalog.Api.Extensions;

public static class RpcExtensions
{
    public static IResult ToProblemDetails(this RpcException ex)
    {
        var (statusCode, title) = ex.StatusCode switch
        {
            StatusCode.InvalidArgument => (StatusCodes.Status400BadRequest, "Invalid argument"),
            StatusCode.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            StatusCode.AlreadyExists => (StatusCodes.Status409Conflict, "Already exists"),
            StatusCode.PermissionDenied => (StatusCodes.Status403Forbidden, "Permission denied"),
            StatusCode.Unauthenticated => (StatusCodes.Status401Unauthorized, "Unauthenticated"),
            StatusCode.FailedPrecondition => (StatusCodes.Status400BadRequest, "Failed precondition"),
            StatusCode.ResourceExhausted => (StatusCodes.Status429TooManyRequests, "Resource exhausted"),
            StatusCode.Unavailable => (StatusCodes.Status503ServiceUnavailable, "Service unavailable"),
            StatusCode.DeadlineExceeded => (StatusCodes.Status504GatewayTimeout, "Deadline exceeded"),
            StatusCode.Cancelled => (StatusCodes.Status499ClientClosedRequest, "Request cancelled"),
            _ => (StatusCodes.Status502BadGateway, "Internal error")
        };

        return Results.Problem(
            title: title,
            detail: ex.Status.Detail,
            statusCode: statusCode);
    }
}