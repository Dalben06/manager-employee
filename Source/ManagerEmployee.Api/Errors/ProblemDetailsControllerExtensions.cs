using ManagerEmployee.Application.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ManagerEmployee.Api.Errors;

internal static class ProblemDetailsControllerExtensions
{
    /// <summary>
    /// Creates a <c>ProblemDetails</c> result through the registered factory (so <c>type</c> and
    /// <c>traceId</c> stay consistent) and appends contract-specific extension members.
    /// </summary>
    public static ObjectResult ProblemWithExtensions(
        this ControllerBase controller,
        int statusCode,
        string title,
        string detail,
        IReadOnlyDictionary<string, object?> extensions)
    {
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext,
            statusCode,
            title,
            detail: detail);

        foreach (var (key, value) in extensions)
        {
            problem.Extensions[key] = value;
        }

        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    /// <summary>
    /// Groups provider validation errors by provider field name for a <c>ValidationProblemDetails</c> body.
    /// </summary>
    public static ModelStateDictionary ToModelState(this IEnumerable<ProviderValidationError> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in errors)
        {
            modelState.AddModelError(error.Field, error.Message);
        }

        return modelState;
    }
}
