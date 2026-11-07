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

// Bounds concurrent parsing: each request decompresses archives and renders images.
builder.Services.AddSingleton<ParserConcurrencyLimiter>();

var app = builder.Build();

app.MapGrpcService<MetadataService>();

app.Run();