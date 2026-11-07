using System.Threading.RateLimiting;
using Auth.Api.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace Auth.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string LoginPolicy = "auth-login";
    public const string RegisterPolicy = "auth-register";
    public const string RefreshPolicy = "auth-refresh";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddAuthRateLimiting(IConfiguration configuration)
        {
            services
                .AddOptions<AuthRateLimitingOptions>()
                .BindConfiguration(AuthRateLimitingOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var options = configuration
                .GetSection(AuthRateLimitingOptions.SectionName)
                .Get<AuthRateLimitingOptions>() ?? new AuthRateLimitingOptions();

            services.AddRateLimiter(rateLimiter =>
            {
                rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                rateLimiter.AddPolicy(LoginPolicy, context => Partition(context, options.LoginPermitLimit, options));
                rateLimiter.AddPolicy(RegisterPolicy, context => Partition(context, options.RegisterPermitLimit, options));
                rateLimiter.AddPolicy(RefreshPolicy, context => Partition(context, options.RefreshPermitLimit, options));

                rateLimiter.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString();
                    }

                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new
                        {
                            type = "https://tools.ietf.org/html/rfc9110#section-15.5.29",
                            title = "Too many requests",
                            status = StatusCodes.Status429TooManyRequests,
                            detail = "Too many authentication attempts. Try again later."
                        },
                        cancellationToken);
                };
            });

            return services;
        }
    }

    private static RateLimitPartition<string> Partition(
        HttpContext context,
        int permitLimit,
        AuthRateLimitingOptions options)
    {
        if (!options.Enabled)
        {
            return RateLimitPartition.GetNoLimiter("disabled");
        }

        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = options.Window,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }
}
