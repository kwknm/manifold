using Aspire.Hosting.Yarp.Transforms;

var builder = DistributedApplication.CreateBuilder(args);

// Project resources must see the same environment as the AppHost so that
// Development runs load user secrets (Jwt:SecretKey for Auth and Catalog).
var environmentName = builder.Environment.EnvironmentName;

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgWeb(p => p.WithHostPort(5050));

var usersDb = postgres.AddDatabase("users-db");
var catalogDb = postgres.AddDatabase("catalog-db");
var storageDb = postgres.AddDatabase("storage-db");

var seaweedS3 = builder.AddSeaweedFS("seaweedfs")
    .WithS3()
    .WithDataVolume();

var authService = builder.AddProject<Projects.Auth_Api>("auth")
    .WithReference(usersDb)
    .WaitFor(usersDb)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", environmentName)
    .WithEnvironment("DOTNET_ENVIRONMENT", environmentName)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// Trust boundary (MVP-CONTRACT §2): storage and bookmetadata are internal-only.
// Do not add .WithExternalHttpEndpoints() to these resources.
var storageService = builder.AddProject<Projects.Storage_Grpc>("storage")
    .WithReference(storageDb)
    .WaitFor(storageDb)
    .WithReference(seaweedS3)
    .WaitFor(seaweedS3);

var bookMetadataService = builder.AddProject<Projects.BookMetadata_Grpc>("bookmetadata")
    .WithReference(storageService)
    .WaitFor(storageService);

var catalogService = builder.AddProject<Projects.Catalog_Api>("catalog")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(storageService)
    .WaitFor(storageService)
    .WithReference(bookMetadataService)
    .WaitFor(bookMetadataService)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", environmentName)
    .WithEnvironment("DOTNET_ENVIRONMENT", environmentName)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var gateway = builder.AddYarp("gateway")
    .WithHostPort(3000)
    // Keep in sync with Shared.UploadLimits.MaxRequestSizeBytes (100 MB + multipart overhead),
    // otherwise the gateway rejects large uploads before they reach Catalog.Api.
    .WithEnvironment("Kestrel__Limits__MaxRequestBodySize", (110L * 1024 * 1024).ToString())
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/api/auth/{**catch-all}", authService)
            .WithTransformPathRemovePrefix("/api/auth");
        
        yarp.AddRoute("/api/catalog/{**catch-all}", catalogService)
            .WithTransformPathRemovePrefix("/api/catalog");
    });

// var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
//     .WithReference(server)
//     .WaitFor(server);

// server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();