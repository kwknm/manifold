using ErrorOr;

namespace Auth.Api.Errors;

public static class AuthErrors
{
    public static class User
    {
        public static Error EmailAlreadyExists => Error.Conflict(
            code: "User.EmailAlreadyExists",
            description: "User with this email already exists.");
    }

    public static class Credentials
    {
        public static Error Invalid => Error.Unauthorized(
            code: "Credentials.Invalid",
            description: "Invalid email or password.");
    }

    public static class RefreshToken
    {
        public static Error Invalid => Error.Unauthorized(
            code: "RefreshToken.Invalid",
            description: "Invalid refresh token.");

        public static Error Expired => Error.Unauthorized(
            code: "RefreshToken.Expired",
            description: "Refresh token has expired.");

        public static Error ReuseDetected => Error.Unauthorized(
            code: "RefreshToken.ReuseDetected",
            description: "Refresh token reuse detected. All sessions in this family were revoked.");
    }
}
