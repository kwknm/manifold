using Auth.Api.Database;
using Auth.Api.Services;
using Carter;
using FluentValidation;
using Shared.Extensions;

namespace Auth.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.AddCarter();

        builder.Services.RegisterJwtOptions();
        builder.Services.RegisterPasswordHashingOptions();
        builder.Services.AddAuthRateLimiting(builder.Configuration);
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();

        builder.Services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        
        builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        builder.Services.AddSingleton<ITokenService, JwtTokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();

        builder.AddNpgsqlDbContext<AuthDbContext>(connectionName: "users-db");

        return builder;
    }
}