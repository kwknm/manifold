using Auth.Api.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Api.Extensions;

public static class OptionsExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection RegisterPasswordHashingOptions()
        {
            services
                .AddOptions<PasswordHashingOptions>()
                .BindConfiguration(PasswordHashingOptions.SectionName)
                .ValidateDataAnnotations()
                .Validate(
                    options => PasswordHashingOptions.SupportedAlgorithms.Contains(
                        options.Algorithm, StringComparer.OrdinalIgnoreCase),
                    $"Algorithm must be one of: {string.Join(", ", PasswordHashingOptions.SupportedAlgorithms)}")
                .ValidateOnStart();

            return services;
        }
    }
}
