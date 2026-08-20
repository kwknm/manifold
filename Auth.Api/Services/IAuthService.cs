using Auth.Api.Contracts;
using ErrorOr;

namespace Auth.Api.Services;

public interface IAuthService
{
    Task<ErrorOr<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<Success>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
