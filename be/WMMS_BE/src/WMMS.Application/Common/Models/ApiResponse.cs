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

    public static ApiResponse<T> Ok(T data, string message = "Operation completed successfully.", string messageCode = MessageCodes.Msg07Success)
        => new()
        {
            Success = true,
            MessageCode = messageCode,
            Message = message,
            Data = data
        };

    public static ApiResponse<T> Fail(string message, string messageCode = MessageCodes.Msg13ValidationError, object? errors = null)
        => new()
        {
            Success = false,
            MessageCode = messageCode,
            Message = message,
            Errors = errors
        };
}

/// <summary>
/// Cấu trúc dữ liệu phân trang chuẩn (Paginated Result) dùng cho danh sách tài khoản (UC-06) và nhật ký kiểm toán (UC-13).
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
