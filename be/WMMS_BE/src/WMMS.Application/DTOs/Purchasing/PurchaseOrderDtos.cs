namespace WMMS.Application.DTOs.Purchasing;

// =============================================================================
// SC-16: Lập Đơn đặt hàng Nhà cung cấp (PO) gom theo Supplier & Theo dõi tiến độ ETA
// UC-49 (Xem PR đã duyệt), UC-50 (Danh sách PO), UC-51 (Chi tiết PO), UC-52 (Tạo PO), UC-53 (Cập nhật lịch giao)
// =============================================================================

/// <summary>Một dòng PR đã được Chủ xưởng duyệt, đang chờ phát hành PO (UC-49, SCR-19).</summary>
public class ApprovedRequisitionLineDto
{
    public long PrItemId { get; set; }
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>Lượng đề xuất mua trong PR (SuggestedOrderQty).</summary>
    public decimal SuggestedOrderQty { get; set; }

    /// <summary>Lượng đã được đặt trong các PO chưa hủy của PR này.</summary>
    public decimal OrderedQty { get; set; }

    /// <summary>Lượng còn có thể đặt = Suggested − Ordered.</summary>
    public decimal RemainingQty { get; set; }
}

/// <summary>Một PR đã duyệt kèm các dòng còn cần đặt hàng (UC-49).</summary>
public class ApprovedRequisitionDto
{
    public long PrId { get; set; }
    public string PrCode { get; set; } = string.Empty;
    public DateTime AggregationDate { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public List<ApprovedRequisitionLineDto> Lines { get; set; } = new();
}

/// <summary>Bộ lọc danh sách PO (UC-50, SCR-19).</summary>
public class PurchaseOrderFilterDto
{
    public long? SupplierId { get; set; }

    /// <summary>ISSUED, PARTIALLY_RECEIVED, FULFILLED, CANCELLED (bỏ trống = tất cả).</summary>
    public string? Status { get; set; }

    /// <summary>Lọc theo tình trạng ETA: OVERDUE (quá hạn chưa về đủ), DUE_SOON (sắp đến hạn).</summary>
    public string? EtaStatus { get; set; }

    public DateTime? FromEta { get; set; }
    public DateTime? ToEta { get; set; }

    /// <summary>Từ khóa theo mã PO, mã PR nguồn, tên NCC hoặc mã/tên vật tư.</summary>
    public string? SearchKeyword { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Dòng tóm tắt PO trong danh sách, kèm tiến độ nhận hàng và tình trạng ETA (UC-50).</summary>
public class PurchaseOrderListItemDto
{
    public long PoId { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public long PrId { get; set; }
    public string? PrCode { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplyCategory { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime ExpectedArrivalDate { get; set; }
    public string PoStatus { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public int LinesCount { get; set; }
    public decimal TotalOrderedQty { get; set; }
    public decimal TotalReceivedQty { get; set; }

    /// <summary>Phần trăm đã nhận = ReceivedQty / OrderedQty × 100 (0..100).</summary>
    public decimal ReceivedPercent { get; set; }

    /// <summary>Số ngày còn lại đến ETA (âm = đã quá hạn).</summary>
    public int EtaDaysRemaining { get; set; }

    /// <summary>ON_TRACK, DUE_SOON, OVERDUE, RECEIVED hoặc CANCELLED.</summary>
    public string EtaStatus { get; set; } = string.Empty;

    // Các trường hiển thị đồng bộ trực tiếp với giao diện Quản lý PO (FE)
    public string PromisedEtaDate => ExpectedArrivalDate.ToString("yyyy-MM-dd");
    public string? EtaDaysLabel { get; set; }
    public string MaterialCategory { get; set; } = string.Empty;
    public string OrderItemsSummary { get; set; } = string.Empty;
    public string FulfillmentStatus { get; set; } = string.Empty;
    public string? SourcePrCode => PrCode;
    public List<MaterialDeliveryScheduleDto> MaterialSchedules { get; set; } = new();
}

/// <summary>Lịch giao sớm nhất của từng nguyên vật liệu trong đơn đặt hàng.</summary>
public class MaterialDeliveryScheduleDto
{
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string ExpectedDeliveryDate { get; set; } = string.Empty;
}

/// <summary>Dòng vật tư của PO (UC-51).</summary>
public class PurchaseOrderLineDto
{
    public long PoDetailId { get; set; }
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialGroup { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal OrderedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal RemainingQty { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? LineAmount { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
}

/// <summary>Phiếu nhập kho liên quan tới PO (UC-51).</summary>
public class PurchaseOrderReceiptDto
{
    public long GrnId { get; set; }
    public string GrnCode { get; set; } = string.Empty;
    public DateTime CommittedAt { get; set; }
    public string? WarehouseKeeperName { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Chi tiết PO đầy đủ (UC-51).</summary>
public class PurchaseOrderDetailDto : PurchaseOrderListItemDto
{
    public string? SupplierCode { get; set; }
    public string? SupplierEmail { get; set; }
    public string? SupplierPhone { get; set; }

    /// <summary>Điều khoản thanh toán &amp; giao hàng của PO (SCR-19 Field #6).</summary>
    public string? Terms { get; set; }

    public long CreatedBy { get; set; }
    public string? CreatorName { get; set; }
    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
    public List<PurchaseOrderReceiptDto> GoodsReceipts { get; set; } = new();
}

/// <summary>Tổng hợp số lượng PO theo trạng thái và ETA cho thẻ KPI SCR-19.</summary>
public class PurchaseOrderSummaryDto
{
    public int IssuedCount { get; set; }
    public int PartiallyReceivedCount { get; set; }
    public int FulfilledCount { get; set; }
    public int CancelledCount { get; set; }
    public int OverdueCount { get; set; }
    public int DueSoonCount { get; set; }
}

/// <summary>Một dòng đặt hàng: lấy từ dòng PR đã duyệt, gán Nhà cung cấp, số lượng và đơn giá thỏa thuận (UC-52).</summary>
public class CreatePoLineDto
{
    public long PrItemId { get; set; }
    public long SupplierId { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
}

/// <summary>Ngày hẹn giao và điều khoản của từng Nhà cung cấp (mỗi NCC một PO).</summary>
public class CreatePoSupplierTermsDto
{
    public long SupplierId { get; set; }

    /// <summary>Ngày NCC cam kết giao hàng; không được ở quá khứ (UC052.E1).</summary>
    public DateTime ExpectedArrivalDate { get; set; }

    /// <summary>Điều khoản thanh toán &amp; giao hàng (SCR-19 Field #6); lưu vào cột <c>purchase_orders.terms</c>.</summary>
    public string? Terms { get; set; }
}

/// <summary>Yêu cầu tạo PO từ PR đã duyệt: hệ thống gom các dòng theo Supplier, mỗi Supplier một PO (UC-52, BR-PUR-03).</summary>
public class CreatePurchaseOrdersRequestDto
{
    public long PrId { get; set; }
    public List<CreatePoLineDto> Lines { get; set; } = new();
    public List<CreatePoSupplierTermsDto> SupplierTerms { get; set; } = new();

    // Các trường tương thích ngược với form Tạo nhanh PO trên FE (nếu FE gửi trực tiếp theo tên NCC)
    public string? SupplierName { get; set; }
    public string? MaterialCategory { get; set; }
    public string? OrderItemsSummary { get; set; }
    public DateTime? PromisedEtaDate { get; set; }
    public string? SourcePrCode { get; set; }
}

/// <summary>Một dòng đặt hàng trong yêu cầu phát hành PO hàng loạt từ nhiều PR đã duyệt.</summary>
public class BatchIssuePoLineDto
{
    public long PrId { get; set; }
    public long PrItemId { get; set; }
    public long SupplierId { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
}

/// <summary>
/// Yêu cầu phát hành đồng loạt các Đơn đặt hàng nhà cung cấp (PO) từ một hoặc nhiều Phiếu yêu cầu mua hàng (PR) đã được Chủ xưởng phê duyệt.
/// Nếu không truyền danh sách dòng chi tiết (<c>Lines</c>), hệ thống sẽ tự động lấy toàn bộ các dòng vật tư còn chờ đặt của các PR đã duyệt,
/// ghép nối với Nhà cung cấp đang hoạt động theo nhóm vật tư và phát hành PO đồng loạt.
/// </summary>
public class BatchIssuePurchaseOrdersRequestDto
{
    public List<long> PrIds { get; set; } = new();
    public List<BatchIssuePoLineDto> Lines { get; set; } = new();
    public List<CreatePoSupplierTermsDto> SupplierTerms { get; set; } = new();

    /// <summary>Tự động gửi email biểu mẫu ĐƠN ĐẶT HÀNG tới các nhà cung cấp vừa được chọn ngay sau khi phát hành.</summary>
    public bool AutoSendEmail { get; set; }

    /// <summary>Địa điểm giao hàng tùy chọn ghi trên biểu mẫu email ĐƠN ĐẶT HÀNG.</summary>
    public string? DeliveryAddress { get; set; }
}

/// <summary>Cập nhật lịch giao hàng của NCC (UC-53).</summary>
public class UpdatePoEtaDto
{
    public DateTime ExpectedArrivalDate { get; set; }

    /// <summary>Trường tương thích với payload <c>promisedEtaDate</c> từ giao diện FE.</summary>
    public DateTime? PromisedEtaDate { get; set; }

    /// <summary>Trạng thái giao nhận tùy chọn gửi kèm từ modal cập nhật ETA trên FE.</summary>
    public string? FulfillmentStatus { get; set; }

    /// <summary>Ghi chú vận chuyển / lý do đổi lịch (lưu vào nhật ký kiểm toán).</summary>
    public string? Notes { get; set; }

    public DateTime EffectiveEtaDate => PromisedEtaDate ?? ExpectedArrivalDate;
}

/// <summary>Cập nhật điều khoản thanh toán &amp; tiến độ cho PO (SC-16).</summary>
public class UpdatePoTermsDto
{
    /// <summary>Điều khoản thanh toán &amp; giao hàng (tối đa 1000 ký tự).</summary>
    public string Terms { get; set; } = string.Empty;

    /// <summary>Ghi chú tiến độ thanh toán / nhật ký kiểm toán.</summary>
    public string? Notes { get; set; }
}

/// <summary>Cập nhật trạng thái vòng đời của PO (SC-16: ISSUED, PARTIALLY_RECEIVED, FULFILLED, CANCELLED).</summary>
public class UpdatePoStatusDto
{
    public string Status { get; set; } = "ISSUED";
    public string? Reason { get; set; }
}

/// <summary>
/// SC-16 / UC-52: Gán nhà cung cấp gửi email cho từng dòng nguyên vật liệu trong Đơn đặt hàng.
/// </summary>
public class SendPoMaterialSupplierAssignmentDto
{
    /// <summary>ID dòng chi tiết vật tư trong đơn đặt hàng (<c>PoDetailId</c>).</summary>
    public long PoDetailId { get; set; }

    /// <summary>Mã nhà cung cấp được người dùng chọn để gửi dòng vật tư này.</summary>
    public long SupplierId { get; set; }
}

/// <summary>
/// SC-16 / UC-52: Yêu cầu gửi email Đơn đặt hàng nguyên vật liệu cho Nhà cung cấp theo biểu mẫu chuẩn.
/// Hỗ trợ chọn nhà cung cấp riêng cho từng loại vật liệu (<see cref="MaterialAssignments"/>) hoặc chọn một <see cref="SupplierId"/> chung.
/// </summary>
public class SendPurchaseOrderEmailDto
{
    /// <summary>Mã nhà cung cấp được chọn thủ công (áp dụng khi gửi toàn bộ đơn cho 1 nhà cung cấp).</summary>
    public long? SupplierId { get; set; }

    /// <summary>Danh sách phân bổ nhà cung cấp cho từng loại vật liệu trong đơn đặt hàng.</summary>
    public List<SendPoMaterialSupplierAssignmentDto>? MaterialAssignments { get; set; }

    /// <summary>Địa chỉ email người nhận (nếu bỏ trống sẽ tự động lấy ContactEmail của nhà cung cấp).</summary>
    public string? RecipientEmail { get; set; }

    /// <summary>Địa điểm giao hàng hiển thị trên mẫu Đơn đặt hàng.</summary>
    public string? DeliveryAddress { get; set; }

    /// <summary>Ghi chú phương thức thanh toán hiển thị trên mẫu Đơn đặt hàng.</summary>
    public string? PaymentMethodNote { get; set; }
}

/// <summary>
/// SC-16 / UC-52: Kết quả gửi email Đơn đặt hàng nguyên vật liệu cho Nhà cung cấp kèm nội dung HTML mẫu Đơn đặt hàng.
/// </summary>
public class SendPurchaseOrderEmailResultDto
{
    public long PoId { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public int DispatchedSupplierCount { get; set; } = 1;
    public bool SentViaSmtp { get; set; }
    public DateTime SentAt { get; set; }
    public string PreviewHtml { get; set; } = string.Empty;
}
