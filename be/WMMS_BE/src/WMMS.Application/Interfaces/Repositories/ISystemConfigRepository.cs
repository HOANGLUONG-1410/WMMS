namespace WMMS.Application.Interfaces.Repositories;

/// <summary>
/// Kho lưu trữ các tham số cấu hình hệ thống toàn cục (<c>system_configs</c>).
/// Phục vụ kiểm tra các ngưỡng nghiệp vụ như tỷ lệ hao hụt gỗ, hạn mức phê duyệt PR/PO, v.v.
/// </summary>
public interface ISystemConfigRepository
{
    /// <summary>
    /// Lấy giá trị tham số cấu hình kiểu số thực (decimal). Nếu không tồn tại sẽ trả về giá trị mặc định.
    /// </summary>
    Task<decimal> GetDecimalConfigAsync(string key, decimal defaultValue);

    /// <summary>
    /// Lấy giá trị tham số cấu hình kiểu chuỗi ký tự (string). Nếu không tồn tại sẽ trả về giá trị mặc định.
    /// </summary>
    Task<string> GetStringConfigAsync(string key, string defaultValue);

    /// <summary>
    /// Lấy giá trị tham số cấu hình kiểu số nguyên (int). Nếu không tồn tại sẽ trả về giá trị mặc định.
    /// </summary>
    Task<int> GetIntConfigAsync(string key, int defaultValue);
}
