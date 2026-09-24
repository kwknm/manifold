using Carter;
using FluentValidation;
using Shared.Clients;
using Catalog.Api.Database;
using Catalog.Api.Services;
using Shared.Extensions;
using Shared.Protos;

namespace Catalog.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.AddCarter();

        builder.Services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        builder.Services.RegisterJwtOptions();
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();

        builder.Services.AddGrpcClient<Files.FilesClient>(o => { o.Address = new Uri("http://storage"); });
        builder.Services.AddGrpcClient<Metadata.MetadataClient>(o => { o.Address = new Uri("http://bookmetadata"); });
        builder.Services.AddScoped<IFilesClient, FilesClient>();
        builder.Services.AddScoped<IMetadataClient, MetadataClient>();
        builder.Services.AddScoped<ICatalogService, CatalogService>();
        builder.Services.AddScoped<ITagService, TagService>();

        builder.AddNpgsqlDbContext<CatalogDbContext>(connectionName: "catalog-db", null,
            options => { options.EnableDetailedErrors(); });

        return builder;
    }
}