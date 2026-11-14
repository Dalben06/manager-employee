using Asp.Versioning;

namespace ManagerEmployee.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTTP transport: controllers, URL-segment API versioning, the versioned OpenAPI
    /// document, and RFC 9457 <c>ProblemDetails</c> for every error response.
    /// </summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddControllers();

        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi();

        // With no IExceptionHandler registered, UseExceptionHandler logs the exception and writes a
        // generic 500 ProblemDetails through this service, without exception details.
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));

        return services;
    }
}
