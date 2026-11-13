using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using Scalar.AspNetCore;

using Wallet.Api.Endpoints;
using Wallet.Api.Http;
using Wallet.Application;
using Wallet.Infrastructure;
using Wallet.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddWalletApplication();
builder.Services.AddWalletInfrastructure();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseConflictHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<WalletDbContext>("postgres", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateWalletDatabaseAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapAccountEndpoints();
app.MapTransferEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();
