using WMMS.Application.DTOs.BomRouting;

namespace WMMS.Application.Interfaces.External;

/// <summary>
/// Giao diện xử lý đọc và phân tích dữ liệu định mức kỹ thuật (BOM) từ tệp Excel (.xlsx).
/// Hỗ trợ nạp dữ liệu tạm thời (Staging Grid) trước khi xác nhận lưu vào hệ thống (UC-14).
/// </summary>
public interface IExcelBomParser
{
    /// <summary>
    /// Đọc luồng dữ liệu Excel và chuyển đổi thành danh sách dòng xem trước BOM (Staging Preview).
    /// </summary>
    /// <param name="fileStream">Luồng dữ liệu tệp Excel tải lên.</param>
    /// <param name="fileName">Tên tệp Excel gốc.</param>
    /// <returns>Đối tượng xem trước định mức BOM kèm trạng thái hợp lệ từng dòng.</returns>
    Task<BomStagingPreviewDto> ParseBomExcelAsync(Stream fileStream, string fileName);
}
