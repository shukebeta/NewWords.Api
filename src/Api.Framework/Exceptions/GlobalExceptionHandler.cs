using System.Net;
using System.Text;
using Api.Framework.Result;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Framework.Exceptions;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Log detailed exception information
        LogDetailedExceptionInfo(context, exception);

        // Set appropriate response properties
        context.Response.ContentType = "application/json";

        // Use appropriate status code instead of always 200 OK
        // You can customize the status code mapping as needed
        context.Response.StatusCode = GetStatusCode(exception);

        // Prepare error result
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
            // Create a more detailed error response
            result = CreateDetailedErrorResult(exception);
        }

        await context.Response.WriteAsJsonAsync(result, cancellationToken);
        return true;
    }

    private void LogDetailedExceptionInfo(HttpContext context, Exception exception)
    {
        // Build a comprehensive log entry
        var logBuilder = new StringBuilder();
        logBuilder.AppendLine("Exception occurred:");
        logBuilder.AppendLine($"Path: {context.Request.Path}");
        logBuilder.AppendLine($"Method: {context.Request.Method}");
        logBuilder.AppendLine($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
        {
            logBuilder.AppendLine($"Correlation ID: {correlationId}");
        }

        logBuilder.AppendLine("Exception details:");
        AppendExceptionDetails(logBuilder, exception, 0);

        logger.LogError(logBuilder.ToString());
    }

    private static void AppendExceptionDetails(StringBuilder builder, Exception exception, int depth)
    {
        if (exception == null) return;

        var indent = new string(' ', depth * 2);

        builder.AppendLine($"{indent}Type: {exception.GetType().FullName}");
        builder.AppendLine($"{indent}Message: {exception.Message}");
        builder.AppendLine($"{indent}Source: {exception.Source}");

        if (exception.TargetSite != null)
        {
            builder.AppendLine($"{indent}Method: {exception.TargetSite.DeclaringType?.FullName}.{exception.TargetSite.Name}");
        }

        builder.AppendLine($"{indent}Stack trace:");
        builder.AppendLine($"{indent}{exception.StackTrace}");

        // For reflection exceptions, try to get additional details
        if (exception is System.Reflection.TargetInvocationException targetInvocationEx)
        {
            builder.AppendLine($"{indent}TargetInvocationException detected. Method being invoked might be throwing an exception.");
        }

        // Add exception data if available
        if (exception.Data.Count > 0)
        {
            builder.AppendLine($"{indent}Exception data:");
            foreach (var key in exception.Data.Keys)
            {
                builder.AppendLine($"{indent}  {key}: {exception.Data[key]}");
            }
        }

        // Process inner exception recursively
        if (exception.InnerException != null)
        {
            builder.AppendLine($"{indent}Inner exception:");
            AppendExceptionDetails(builder, exception.InnerException, depth + 1);
        }

        // For aggregate exceptions, process all inner exceptions
        if (exception is AggregateException aggregateEx)
        {
            builder.AppendLine($"{indent}Aggregate exceptions ({aggregateEx.InnerExceptions.Count}):");
            int index = 0;
            foreach (var innerEx in aggregateEx.InnerExceptions)
            {
                builder.AppendLine($"{indent}Inner exception #{++index}:");
                AppendExceptionDetails(builder, innerEx, depth + 1);
            }
        }
    }

    private ApiResult CreateDetailedErrorResult(Exception exception)
    {
        // In development environment, provide detailed error information
        // In production, provide minimal information
        if (environment.IsDevelopment())
        {
            // Create a custom FailedResult with more details for development
            var result = new FailedResult(exception.Message);

            // Add additional properties here if your FailedResult supports them
            // For example, if you can add exception details to your FailedResult:
            // result.Details = GetExceptionDetails(exception);

            return result;
        }

        // In production, return a generic error message
        return new FailedResult("An error occurred while processing your request.");
    }

    private static int GetStatusCode(Exception exception)
    {
        // Map exceptions to appropriate HTTP status codes
        return exception switch
        {
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            // You could add more mappings for other exception types
            _ => (int)HttpStatusCode.InternalServerError // Default to 500 for unhandled exception types
        };
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
