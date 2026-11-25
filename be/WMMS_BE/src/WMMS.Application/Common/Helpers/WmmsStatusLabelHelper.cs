namespace WMMS.Application.Common.Helpers;

/// <summary>
/// Bộ chuyển đổi tập trung các mã trạng thái, nhóm vật tư, loại giao dịch và mức độ rủi ro sang nhãn Tiếng Việt chuẩn hóa (GBR-i18n).
/// Giúp các DTO của Backend tự động cung cấp kèm trường <c>*Label</c> để Frontend chỉ việc hiển thị trực tiếp, không cần tự dịch.
/// </summary>
public static class WmmsStatusLabelHelper
{
    private static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Nhãn trạng thái sẵn sàng vật tư tổng thể của MRP (SC-14).</summary>
    public static string ToOverallReadinessLabel(string? code) => Normalize(code) switch
    {
        "READY_TO_PRODUCE" => "Đã đủ vật tư",
        "WAITING_FOR_MATERIAL" or "WAITING_FOR_MATERIALS" => "Chưa đủ nguyên liệu",
        _ => string.IsNullOrWhiteSpace(code) ? "Chưa phân tích" : code
    };

    /// <summary>Nhãn trạng thái phân bổ / giữ chỗ vật tư trong kho (SC-12, SC-14, SC-18).</summary>
    public static string ToAllocationStatusLabel(string? code) => Normalize(code) switch
    {
        "RESERVED" or "FULL" or "FULLY_ALLOCATED" => "Đã giữ chỗ",
        "PARTIAL" or "PARTIALLY_ALLOCATED" or "PARTIALLY_RESERVED" => "Chưa đủ nguyên liệu",
        "ISSUED_TO_SHOP" or "ISSUED" => "Đã xuất xưởng",
        "RELEASED" => "Đã hủy giữ chỗ",
        "STOCK_AVAILABLE" or "SUFFICIENT" => "Đủ tồn kho",
        "SHORTAGE" or "WAITING" or "WAITING_MATERIAL" or "PENDING" => "Chưa đủ nguyên liệu",
        _ => string.IsNullOrWhiteSpace(code) ? "Chưa phân bổ" : code
    };

    /// <summary>Nhãn nhóm nguyên vật liệu chuẩn (BR-MDM-01).</summary>
    public static string ToMaterialGroupLabel(string? code) => Normalize(code) switch
    {
        "LUMBER" or "TIMBER" => "Gỗ nguyên liệu",
        "HARDWARE" => "Phụ kiện kim khí",
        "FINISH_PAINT" or "PAINT" => "Sơn & hoàn thiện",
        "PACKAGING" or "CARTON" => "Bao bì đóng gói",
        _ => string.IsNullOrWhiteSpace(code) ? "Vật tư khác" : code
    };

    /// <summary>Nhãn loại giao dịch sổ kho điện tử (SC-12).</summary>
    public static string ToTxTypeLabel(string? code) => Normalize(code) switch
    {
        "INITIAL_IMPORT" => "Tồn đầu kỳ",
        "IN_RECEIPT" => "Nhập kho mua hàng",
        "OUT_ISSUE" => "Xuất kho sản xuất",
        "ADJUST" => "Điều chỉnh kiểm kê",
        _ => string.IsNullOrWhiteSpace(code) ? "Giao dịch kho" : code
    };

    /// <summary>Nhãn trạng thái vòng đời đơn hàng B2B (SC-08, SC-09).</summary>
    public static string ToOrderStatusLabel(string? code) => Normalize(code) switch
    {
        "DRAFT" => "Bản nháp",
        "CONFIRMED" or "NEWLY_RECEIVED" => "Mới tiếp nhận",
        "IN_PLANNING" or "BOM_SETTING" => "Đang lập kế hoạch",
        "IN_PRODUCTION" => "Đang sản xuất",
        "COMPLETED" => "Hoàn thành",
        "CANCELLED" => "Đã hủy",
        _ => string.IsNullOrWhiteSpace(code) ? "Đang xử lý" : code
    };

    /// <summary>Nhãn trạng thái dòng sản phẩm đơn hàng và định mức BOM (SC-09, SC-10, SC-11).</summary>
    public static string ToOrderItemAndBomLabel(string? code) => Normalize(code) switch
    {
        "NOT_CREATED" or "PENDING_BOM" or "PENDING" => "Chưa lập BOM",
        "DRAFT" or "BOM_DRAFT" => "BOM nháp",
        "PENDING_APPROVAL" => "Chờ duyệt BOM",
        "APPROVED" or "BOM_APPROVED" => "Đã duyệt BOM",
        "WAITING_MAT" or "WAITING_FOR_MATERIALS" => "Chờ vật tư",
        "READY_PROD" or "READY_FOR_PRODUCTION" or "LOCKED" => "Sẵn sàng sản xuất",
        "IN_PROD" => "Đang sản xuất",
        "DONE" => "Hoàn thành",
        _ => string.IsNullOrWhiteSpace(code) ? "Chưa lập BOM" : code
    };

    /// <summary>Nhãn trạng thái Phiếu yêu cầu mua hàng tổng hợp PR (SC-15).</summary>
    public static string ToPrStatusLabel(string? code) => Normalize(code) switch
    {
        "PENDING_APPROVAL" => "Chờ phê duyệt",
        "APPROVED" => "Đã phê duyệt",
        "REJECTED" => "Đã từ chối",
        "PO_ISSUED" => "Đã lập đơn mua",
        _ => string.IsNullOrWhiteSpace(code) ? "Chờ xử lý" : code
    };

    /// <summary>Nhãn trạng thái Đơn đặt hàng nhà cung cấp PO (SC-16).</summary>
    public static string ToPoStatusLabel(string? code) => Normalize(code) switch
    {
        "ISSUED" => "Đã phát hành",
        "PARTIALLY_RECEIVED" => "Nhận một phần",
        "FULFILLED" or "RECEIVED" or "COMPLETED" => "Đã nhận đủ",
        "CANCELLED" => "Đã hủy",
        _ => string.IsNullOrWhiteSpace(code) ? "Đã phát hành" : code
    };

    /// <summary>Nhãn trạng thái Phiếu nhập kho GRN (SC-17).</summary>
    public static string ToGrnStatusLabel(string? code) => Normalize(code) switch
    {
        "COMMITTED" => "Đã nhập kho",
        "AUTO_ALLOCATED" => "Đã nhập & giữ chỗ tự động",
        _ => string.IsNullOrWhiteSpace(code) ? "Đã nhập kho" : code
    };

    /// <summary>Nhãn trạng thái Lô sản xuất tại xưởng (SC-19).</summary>
    public static string ToBatchStatusLabel(string? code) => Normalize(code) switch
    {
        "LOCKED" => "Chờ xuất kho vật tư",
        "IN_PROGRESS" => "Đang gia công",
        "QC_PENDING" => "Chờ kiểm tra KCS",
        "HOLD_POINT_BLOCKED" => "Tạm dừng tại chốt kiểm soát",
        "DONE" => "Hoàn thành",
        _ => string.IsNullOrWhiteSpace(code) ? "Đang xử lý" : code
    };

    /// <summary>Nhãn phân loại Lô sản xuất (SC-19, SC-20).</summary>
    public static string ToBatchTypeLabel(string? code) => Normalize(code) switch
    {
        "MAIN_CARD" => "Lô sản xuất chính",
        "REWORK_SUB_CARD" => "Lô sửa chữa",
        "REPLENISH_LOT" => "Lô sản xuất bù",
        _ => string.IsNullOrWhiteSpace(code) ? "Lô sản xuất" : code
    };

    /// <summary>Nhãn trạng thái Công đoạn gia công của lô sản xuất (SC-19).</summary>
    public static string ToStepStatusLabel(string? code) => Normalize(code) switch
    {
        "WAITING" => "Chờ thực hiện",
        "IN_PROGRESS" => "Đang gia công",
        "QC_PENDING" => "Chờ kiểm tra KCS",
        "PASSED" => "Đã đạt KCS",
        "BLOCKED" => "Đang tạm dừng",
        _ => string.IsNullOrWhiteSpace(code) ? "Chờ thực hiện" : code
    };

    /// <summary>Nhãn hướng xử lý lỗi KCS (SC-20).</summary>
    public static string ToDispositionLabel(string? code) => Normalize(code) switch
    {
        "REWORK_SUB_CARD" => "Sửa chữa tại trạm",
        "SCRAP_REPLENISH" => "Hủy phế phẩm & làm bù",
        _ => string.IsNullOrWhiteSpace(code) ? "Không có lỗi" : code
    };

    /// <summary>Nhãn trạng thái Tài khoản người dùng (UC-06..UC-10).</summary>
    public static string ToUserStatusLabel(string? code) => Normalize(code) switch
    {
        "ACTIVE" => "Đang hoạt động",
        "INACTIVE" => "Ngừng kích hoạt",
        "BANNED" => "Đã khóa vĩnh viễn",
        "LOCKED" => "Tạm khóa (30 phút)",
        _ => string.IsNullOrWhiteSpace(code) ? "Không xác định" : code
    };

    /// <summary>Nhãn trạng thái Danh mục Master Data (Product, Material, Supplier).</summary>
    public static string ToMasterStatusLabel(string? code) => Normalize(code) switch
    {
        "ACTIVE" => "Đang sử dụng",
        "INACTIVE" or "DEACTIVATED" => "Ngừng sử dụng",
        _ => string.IsNullOrWhiteSpace(code) ? "Đang sử dụng" : code
    };

    /// <summary>Nhãn mức độ rủi ro trễ hạn giao hàng ETD (BR-012, UC-77).</summary>
    public static string ToEtdRiskLevelLabel(string? code) => Normalize(code) switch
    {
        "ON_TRACK" => "Đúng tiến độ",
        "WARNING" => "Sắp đến hạn giao",
        "OVERDUE" => "Trễ hạn giao",
        "COMPLETED" => "Đã hoàn thành",
        _ => string.IsNullOrWhiteSpace(code) ? "Đúng tiến độ" : code
    };
}
