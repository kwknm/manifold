using Aspire.Hosting.Yarp.Transforms;

var builder = DistributedApplication.CreateBuilder(args);

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
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

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
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var gateway = builder.AddYarp("gateway")
    .WithHostPort(3000)
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