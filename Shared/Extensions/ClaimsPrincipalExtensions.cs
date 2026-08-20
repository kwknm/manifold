using System.Security.Claims;

namespace Shared.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirst(ClaimTypes.NameIdentifier)
                          ?? throw new UnauthorizedAccessException("User ID claim not found");

        return Guid.TryParse(userIdValue.Value, out var userId)
            ? userId
            : throw new FormatException($"Invalid user ID format: {userIdValue}");
    }

    public static Guid? TryGetUserId(this ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirst(ClaimTypes.NameIdentifier);

        var ok = Guid.TryParse(userIdValue?.Value, out var userId);
        return ok ? userId : null;
    }
}