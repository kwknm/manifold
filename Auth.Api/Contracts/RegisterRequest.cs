namespace Auth.Api.Contracts;

public sealed record RegisterRequest(
    string Email,
    string UserName,
    string Password);
