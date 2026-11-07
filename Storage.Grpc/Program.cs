using Shared.Extensions;
using Storage.Grpc.Database;
using Storage.Grpc.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.RegisterOptions();
builder.AddDatabase();
builder.Services.AddGrpc();
builder.AddSeaweedFsClient();

var app = builder.Build();

app.ApplyMigrations<StorageDbContext>();

app.MapGrpcServices();

app.Run();