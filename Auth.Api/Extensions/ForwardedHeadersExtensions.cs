using System.Net;
using Auth.Api.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace Auth.Api.Extensions;

public static class ForwardedHeadersExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Enables forwarded-header processing for the proxies listed in the
        /// <see cref="ForwardedHeadersSettings.SectionName"/> section. Without it the per-IP
        /// rate limit partitions every request behind the gateway into a single bucket.
        /// </summary>
        public IServiceCollection AddTrustedProxyHeaders(IConfiguration configuration)
        {
            services
                .AddOptions<ForwardedHeadersSettings>()
                .BindConfiguration(ForwardedHeadersSettings.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var settings = configuration
                .GetSection(ForwardedHeadersSettings.SectionName)
                .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

            if (!settings.Enabled)
            {
                return services;
            }

            // Parsed eagerly so a typo in configuration fails at startup instead of silently
            // leaving the rate limiter with a bucket nobody matches.
            var proxies = settings.KnownProxies.Select(ParseProxy).ToArray();
            var networks = settings.KnownNetworks.Select(ParseNetwork).ToArray();

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = settings.ForwardLimit;

                if (proxies.Length > 0 || networks.Length > 0)
                {
                    // Replaces the framework defaults (loopback) rather than extending them:
                    // trusting loopback on top of an explicit list would widen, not narrow, the
                    // set of callers whose header is believed.
                    options.KnownProxies.Clear();
                    options.KnownIPNetworks.Clear();
                }

                foreach (var proxy in proxies)
                {
                    options.KnownProxies.Add(proxy);
                }

                foreach (var network in networks)
                {
                    options.KnownIPNetworks.Add(network);
                }
            });

            return services;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Must run before anything reads <see cref="HttpContext.Connection"/>, in particular
        /// the rate limiter, which partitions on the client address.
        /// </summary>
        public WebApplication UseTrustedProxyHeaders()
        {
            if (!app.Services.GetRequiredService<IOptions<ForwardedHeadersSettings>>().Value.Enabled)
            {
                return app;
            }

            app.UseForwardedHeaders();

            return app;
        }
    }

    private static IPAddress ParseProxy(string value) =>
        IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException(
                $"{ForwardedHeadersSettings.SectionName}:KnownProxies entry '{value}' is not a valid IP address.");

    private static IPNetwork ParseNetwork(string value) =>
        IPNetwork.TryParse(value, out var network)
            ? network
            : throw new InvalidOperationException(
                $"{ForwardedHeadersSettings.SectionName}:KnownNetworks entry '{value}' is not a valid CIDR network.");
}
