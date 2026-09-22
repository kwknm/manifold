using BookMetadata.Grpc.Services;
using Shared.Protos;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddGrpc();
builder.Services.AddHttpClient();
builder.Services.AddGrpcClient<Files.FilesClient>(o =>
{
    o.Address = new Uri("http://storage");
});

var app = builder.Build();

app.MapGrpcService<MetadataService>();

app.Run();