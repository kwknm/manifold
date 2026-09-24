using Storage.Grpc.Database;
using Storage.Grpc.Options;
using Storage.Grpc.Services;

namespace Storage.Grpc.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection RegisterOptions()
        {
            services.AddOptions<SeaweedOptions>()
                .BindConfiguration(SeaweedOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }
    }

    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapGrpcServices()
        {
            app.MapGrpcService<StorageService>();
            
            return app;
        }
    }

    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddDatabase()
        {
            builder.AddNpgsqlDbContext<StorageDbContext>(connectionName: "storage-db");

            return builder;
        }

        public WebApplicationBuilder AddSeaweedFsClient()
        {
            builder.AddSeaweedFSS3Client("seaweedfs");

            return builder;
        }
    }
}