using ManagerEmployee.Application.Employees.Queries.GetEmployee;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Infrastructure;
using ManagerEmployee.Infrastructure.Provider1.Integration.Providers;
using ManagerEmployee.Infrastructure.Provider2.Integration.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IProviderEmployeeMapper, Provider1EmployeeMapper>();
builder.Services.AddScoped<IProviderEmployeeMapper, Provider2EmployeeMapper>();
builder.Services.AddScoped<IEmployeeMapperFactory, EmployeeMapperFactory>();
builder.Services.AddScoped<GetEmployeeQueryHandler>();
builder.Services.AddSingleton<EmployeeSynchronizationCoordinator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("ManagerEmployeeDatabase")
        ?? throw new InvalidOperationException(
            "Connection string 'ManagerEmployeeDatabase' is required."));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

/// <summary>
/// Exposes the generated application entry point to integration tests.
/// </summary>
public partial class Program;
