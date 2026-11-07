using Auth.Api.Database;
using Auth.Api.Extensions;
using Carter;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddServices();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.ApplyMigrations<AuthDbContext>();

// Must precede the rate limiter: it partitions on Connection.RemoteIpAddress.
app.UseTrustedProxyHeaders();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapCarter();

app.Run();
