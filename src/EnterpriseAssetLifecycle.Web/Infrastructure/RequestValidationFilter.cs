using System.ComponentModel.DataAnnotations;

namespace EnterpriseAssetLifecycle.Infrastructure;

public sealed class RequestValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return await next(context);
        }

        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(model, new ValidationContext(model), validationResults, true))
        {
            return await next(context);
        }

        var errors = validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => new { Member = member, result.ErrorMessage }))
            .GroupBy(item => item.Member)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ErrorMessage ?? "Invalid value.").ToArray());
        return Results.ValidationProblem(errors);
    }
}

