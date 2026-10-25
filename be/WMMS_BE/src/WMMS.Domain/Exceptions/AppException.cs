using WMMS.Domain.Constants;

namespace WMMS.Domain.Exceptions;

/// <summary>
/// Ngoại lệ nghiệp vụ chuẩn của hệ thống WMMS (Domain/Application Exception).
/// Được bắt tự động bởi <c>GlobalExceptionMiddleware</c> để trả về JSON chuẩn kèm HTTP Status Code và MessageCode (MSG01..MSG13).
/// </summary>
public class AppException : Exception
{
    /// <summary>Mã trạng thái HTTP trả về (ví dụ: 400, 401, 403, 404, 409, 423, 429).</summary>
    public int StatusCode { get; }

    /// <summary>Mã thông báo chuẩn theo đặc tả Report 3 (ví dụ: MSG02, MSG03, MSG08).</summary>
    public string MessageCode { get; }

    /// <summary>Chi tiết lỗi bổ sung (nếu có).</summary>
    public object? Errors { get; }

    public AppException(
        string message,
        int statusCode = 400,
        string messageCode = MessageCodes.Msg13ValidationError,
        object? errors = null) : base(message)
    {
        StatusCode = statusCode;
        MessageCode = messageCode;
        Errors = errors;
    }

    public static AppException Unauthorized(string message, string messageCode = MessageCodes.Msg02InvalidCredentials)
        => new(message, 401, messageCode);

    public static AppException Forbidden(string message, string messageCode = MessageCodes.Msg11ForbiddenRule)
        => new(message, 403, messageCode);

    public static AppException NotFound(string message, string messageCode = MessageCodes.Msg10NotFound)
        => new(message, 404, messageCode);

    public static AppException Conflict(string message, string messageCode = MessageCodes.Msg08DuplicateConflict)
        => new(message, 409, messageCode);

    public static AppException Locked(string message, string messageCode = MessageCodes.Msg03AccountLocked)
        => new(message, 423, messageCode);

    public static AppException TooManyRequests(string message, string messageCode = MessageCodes.Msg09RateLimitExceeded)
        => new(message, 429, messageCode);
}
