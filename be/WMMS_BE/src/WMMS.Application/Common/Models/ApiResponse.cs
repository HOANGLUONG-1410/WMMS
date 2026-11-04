using WMMS.Domain.Constants;

namespace WMMS.Application.Common.Models;

/// <summary>
/// Phong bì phản hồi API chuẩn (Standard API Response Envelope) cho toàn bộ hệ thống WMMS.
/// Giúp Frontend nhận diện nhất quán trạng thái thành công/thất bại, mã thông báo (MSG01..MSG13) và dữ liệu trả về.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string MessageCode { get; set; } = MessageCodes.Msg07Success;
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public object? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string message = "Success", string messageCode = MessageCodes.Msg07Success)
    {
        return new ApiResponse<T>
        {
            Success = true,
            MessageCode = messageCode,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> Fail(string message, List<string>? errors = null, string messageCode = MessageCodes.Msg13ValidationError)
    {
        return new ApiResponse<T>
        {
            Success = false,
            MessageCode = messageCode,
            Message = message,
            Errors = errors ?? new List<string>()
        };
    }

    public static ApiResponse<T> Fail(string message, string messageCode, object? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            MessageCode = messageCode,
            Message = message,
            Errors = errors
        };
    }
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string message = "Success", string messageCode = MessageCodes.Msg07Success)
    {
        return new ApiResponse
        {
            Success = true,
            MessageCode = messageCode,
            Message = message,
            Data = null
        };
    }

    public static new ApiResponse Fail(string message, List<string>? errors = null, string messageCode = MessageCodes.Msg13ValidationError)
    {
        return new ApiResponse
        {
            Success = false,
            MessageCode = messageCode,
            Message = message,
            Errors = errors ?? new List<string>()
        };
    }
}
