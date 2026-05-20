using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mets.Replenishment.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var statusCode = HttpStatusCode.InternalServerError;
        var message = "An unexpected error occurred.";

        switch (exception)
        {
            case KeyNotFoundException keyNotFoundEx:
                statusCode = HttpStatusCode.NotFound;
                message = keyNotFoundEx.Message;
                _logger.LogWarning(keyNotFoundEx, "Resource not found on path {Path}", context.Request.Path);
                break;

            case InvalidOperationException invalidOpEx:
                statusCode = HttpStatusCode.BadRequest;
                message = invalidOpEx.Message;
                _logger.LogWarning(invalidOpEx, "Validation or business rule violation on path {Path}", context.Request.Path);
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                message = "An unexpected server error occurred.";
                _logger.LogError(exception, "Unhandled system error occurred on path {Path}", context.Request.Path);
                break;
        }

        response.StatusCode = (int)statusCode;

        var result = JsonSerializer.Serialize(new { error = message });
        await response.WriteAsync(result);
    }
}
