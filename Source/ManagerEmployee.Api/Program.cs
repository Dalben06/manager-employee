using Asp.Versioning.Builder;
using ManagerEmployee.Api.Configuration;
using ManagerEmployee.Application;
using ManagerEmployee.Infrastructure;
using ManagerEmployee.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi().WithDocumentPerVersion();
app.MapControllers();

await app.RunAsync();

/// <summary>
/// Exposes the generated application entry point to integration tests.
/// </summary>
public partial class Program;
