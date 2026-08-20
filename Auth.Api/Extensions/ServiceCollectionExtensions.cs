using System.Text;
using Auth.Api.Database;
using Auth.Api.Options;
using Auth.Api.Services;
using Carter;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.AddCarter();

        builder.Services.RegisterOptions();
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();

        builder.Services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        
        builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        builder.Services.AddSingleton<ITokenService, JwtTokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();

        builder.AddNpgsqlDbContext<AuthDbContext>(connectionName: "users-db");

        return builder;
    }

    // TODO: move to Shared
    extension(IServiceCollection services)
    {
        private IServiceCollection RegisterOptions()
        {
            services
                .AddOptions<JwtOptions>()
                .BindConfiguration(JwtOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }

        private IServiceCollection AddJwtAuthentication(IConfiguration configuration)
        {
            var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                });

            return services;
        }
    }
}