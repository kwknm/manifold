using Carter;
using Catalog.Api.Clients;
using Catalog.Api.Database;
using Catalog.Api.Services;
using Storage.Grpc;

namespace Catalog.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.AddCarter();

        // builder.Services.AddJwtAuthentication(builder.Configuration);
        // builder.Services.AddAuthorization();

        builder.Services.AddGrpcClient<Files.FilesClient>(o => { o.Address = new Uri("http://storage"); });
        builder.Services.AddScoped<IFilesClient, FilesClient>();
        builder.Services.AddScoped<ICatalogService, CatalogService>();

        builder.AddNpgsqlDbContext<CatalogDbContext>(connectionName: "catalog-db", null,
            options => { options.EnableDetailedErrors(); });

        return builder;
    }
}