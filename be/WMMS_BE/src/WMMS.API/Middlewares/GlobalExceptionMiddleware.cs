using System.Net;
using System.Text.Json;
using FluentValidation;
using WMMS.Application.Common.Models;
using WMMS.Domain.Constants;
using WMMS.Domain.Exceptions;

namespace WMMS.API.Middlewares;

/// <summary>
/// Middleware bắt và chuẩn hóa toàn bộ ngoại lệ phát sinh trong hệ thống WMMS.
/// Đảm bảo mọi phản hồi lỗi đều tuân theo phong bì JSON <see cref="ApiResponse{T}"/> kèm HTTP Status Code và MessageCode (MSG01..MSG13).
/// </summary>
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = (int)HttpStatusCode.InternalServerError;
        var messageCode = MessageCodes.Msg13ValidationError;
        var message = "An unexpected server error occurred.";
        object? errors = null;

        switch (exception)
        {
            case AppException appEx:
                statusCode = appEx.StatusCode;
                messageCode = appEx.MessageCode;
                message = appEx.Message;
                errors = appEx.Errors;
                break;

            case ValidationException valEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                messageCode = MessageCodes.Msg13ValidationError;
                message = "One or more validation errors occurred.";
                errors = valEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                break;

            case UnauthorizedAccessException unauthEx:
                statusCode = (int)HttpStatusCode.Unauthorized;
                messageCode = MessageCodes.Msg02InvalidCredentials;
                message = unauthEx.Message;
                break;

            default:
                _logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var responsePayload = ApiResponse<object>.Fail(message, messageCode, errors);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(responsePayload, jsonOptions));
    }
}
