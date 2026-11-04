using System.Net;
using System.Text.Json;
using FluentValidation;
using WMMS.Application.Common.Models;
using WMMS.Domain.Exceptions;

namespace WMMS.API.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            if (ex is AppException or DomainException or ValidationException)
            {
                _logger.LogWarning("Handled business/validation exception ({ExceptionType}): {Message}", ex.GetType().Name, ex.Message);
            }
            else
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ApiResponse<object>();
        var statusCode = HttpStatusCode.InternalServerError;

        switch (exception)
        {
            case AppException appEx:
                statusCode = (HttpStatusCode)appEx.StatusCode;
                response = appEx.Errors is not null
                    ? ApiResponse<object>.Fail(appEx.Message, appEx.MessageCode, appEx.Errors)
                    : ApiResponse<object>.Fail(
                        appEx.Message,
                        !string.IsNullOrWhiteSpace(appEx.MessageCode) ? new List<string> { appEx.MessageCode } : new List<string>(),
                        appEx.MessageCode);
                break;

            case NotFoundException notFoundEx:
                statusCode = HttpStatusCode.NotFound;
                response = ApiResponse<object>.Fail(notFoundEx.Message);
                break;

            case ActiveBomConflictException conflictEx:
                statusCode = HttpStatusCode.Conflict;
                response = ApiResponse<object>.Fail(conflictEx.Message, new List<string> { conflictEx.ErrorCode });
                break;

            case InvalidScrapRateException scrapEx:
                statusCode = HttpStatusCode.BadRequest;
                response = ApiResponse<object>.Fail(scrapEx.Message, new List<string> { scrapEx.ErrorCode });
                break;

            case StagingValidationException stagingEx:
                statusCode = HttpStatusCode.BadRequest;
                response = ApiResponse<object>.Fail(stagingEx.Message, new List<string> { stagingEx.ErrorCode });
                break;

            case ValidationException valEx:
                statusCode = HttpStatusCode.BadRequest;
                var errors = valEx.Errors.Select(e => e.ErrorMessage).ToList();
                response = ApiResponse<object>.Fail("Validation failed.", errors);
                break;

            case DomainException domainEx:
                statusCode = HttpStatusCode.BadRequest;
                response = ApiResponse<object>.Fail(domainEx.Message, new List<string> { domainEx.ErrorCode });
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                response = ApiResponse<object>.Fail("An unexpected internal server error occurred.");
                break;
        }

        context.Response.StatusCode = (int)statusCode;
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
