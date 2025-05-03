using System.Net;
using Api.Framework.Result;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Api.Framework.Exceptions;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception.ToString());
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        ApiResult result;
        if (IsCustomException(exception))
        {
            var customExceptionType = exception.GetType();
            var dataProperty = customExceptionType.GetProperty("CustomData");

            if (dataProperty != null)
            {
                var failedResult = dataProperty.GetValue(exception);
                await context.Response.WriteAsJsonAsync(failedResult, cancellationToken);
                return true;
            }

            result = new FailedResult(exception.Message);
        }
        else
        {
            result = new FailedResult(exception.Message);
        }

        await context.Response.WriteAsJsonAsync(result, cancellationToken);
        return true;
    }

    private static bool IsCustomException(Exception ex)
    {
        var customExceptionType = typeof(CustomException<>);

        var exType = ex.GetType();
        while (exType != null && exType != typeof(object))
        {
            if (exType.IsGenericType && exType.GetGenericTypeDefinition() == customExceptionType)
            {
                return true;
            }

            exType = exType.BaseType;
        }

        return false;
    }
}
