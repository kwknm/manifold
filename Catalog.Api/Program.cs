using Carter;
using Catalog.Api.Database;
using Catalog.Api.Extensions;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.AddServices();

var app = builder.Build();

app.ApplyMigrations<CatalogDbContext>();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseAuthentication();
// app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapCarter();

app.Run();