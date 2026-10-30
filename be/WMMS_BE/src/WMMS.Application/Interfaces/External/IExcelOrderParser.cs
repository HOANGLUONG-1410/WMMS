using WMMS.Application.DTOs.Orders;

namespace WMMS.Application.Interfaces.External;

/// <summary>
/// Interface bộ phân tích file Excel PO Khách hàng độc lập (UC-30: Upload Customer PO Excel, BR-ORD-01).
/// Tách biệt hoàn toàn tầng xử lý file Excel (EPPlus) khỏi tầng dịch vụ nghiệp vụ OrderIntakeService.
/// </summary>
public interface IExcelOrderParser
{
    Task<ExcelOrderParseResultDto> ParseOrderExcelAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
