using Microsoft.Extensions.DependencyInjection;
using Shared.Options;

namespace Shared.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection RegisterJwtOptions()
        {
            services
                .AddOptions<JwtOptions>()
                .BindConfiguration(JwtOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }
    }
}