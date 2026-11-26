import { apiClient } from "../../../lib/axios";
import type {
  CreateSupplierPoDto,
  PurchaseOrderQueryParams,
  SupplierPurchaseOrderDto,
  UpdatePoEtaDto,
} from "../types";

export interface PoLineItemDto {
  poDetailId: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  materialGroup?: string;
  unitOfMeasure: string;
  orderedQty: number;
  receivedQty: number;
  remainingQty: number;
  unitPrice?: number | null;
  lineAmount?: number | null;
}

export interface PurchaseOrderDetailViewDto extends SupplierPurchaseOrderDto {
  supplierId?: number;
  supplierCode?: string | null;
  supplierEmail?: string | null;
  supplierPhone?: string | null;
  terms?: string | null;
  totalAmount?: number | null;
  lines: PoLineItemDto[];
}

export interface ActiveSupplierOptionDto {
  supplierId: number;
  supplierCode: string;
  supplierName: string;
  supplyCategory?: string | null;
  contactEmail?: string | null;
  phone?: string | null;
  status: string;
}

export interface SendPoMaterialAssignmentDto {
  poDetailId: number;
  supplierId: number;
}

export interface ApprovedRequisitionLineItemDto {
  prItemId: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  materialGroup: string;
  unitOfMeasure: string;
  suggestedOrderQty: number;
  orderedQty: number;
  remainingQty: number;
}

export interface ApprovedRequisitionViewDto {
  prId: number;
  prCode: string;
  aggregationDate: string;
  reviewedAt?: string | null;
  lines: ApprovedRequisitionLineItemDto[];
}

export interface DistributeMaterialLinePayloadDto {
  prId: number;
  prItemId: number;
  supplierId: number;
  orderedQty: number;
  unitPrice: number;
  expectedArrivalDate?: string;
  materialName?: string;
  materialCode?: string;
  materialGroup?: string;
  unitOfMeasure?: string;
}

export interface SendPoEmailRequestDto {
  supplierId?: number;
  materialAssignments?: SendPoMaterialAssignmentDto[];
  recipientEmail?: string;
  deliveryAddress?: string;
  paymentMethodNote?: string;
}

export interface SendPoEmailResponseDto {
  poId: number;
  poCode: string;
  supplierId: number;
  supplierName: string;
  recipientEmail: string;
  sentViaSmtp: boolean;
  sentAt: string;
  previewHtml: string;
}

export const purchasingApi = {
  /** UC-50: Truy vấn danh sách đơn mua hàng nhà cung cấp (GET /api/v1/purchase-orders) */
  getPurchaseOrders: async (
    params: PurchaseOrderQueryParams = {},
  ): Promise<SupplierPurchaseOrderDto[]> => {
    const res = await apiClient.get<{
      success: boolean;
      data:
        | SupplierPurchaseOrderDto[]
        | { items?: Array<SupplierPurchaseOrderDto & { expectedArrivalDate?: string }> };
    }>("/purchase-orders", { params });

    const payload = res.data?.data;
    const rawItems = Array.isArray(payload)
      ? payload
      : Array.isArray(payload?.items)
        ? payload.items
        : [];

    return rawItems.map((po) => ({
      ...po,
      issueDate: String(po.issueDate || "").slice(0, 10),
      promisedEtaDate: String(
        po.promisedEtaDate ||
          (po as { expectedArrivalDate?: string }).expectedArrivalDate ||
          "",
      ).slice(0, 10),
    }));
  },

  /** UC-51: Xem chi tiết đơn đặt hàng kèm danh sách mặt hàng nguyên vật liệu (GET /api/v1/purchase-orders/:id) */
  getPurchaseOrderById: async (
    poId: number,
    _fallbackPo?: SupplierPurchaseOrderDto | null,
  ): Promise<PurchaseOrderDetailViewDto> => {
    const res = await apiClient.get<{
      success: boolean;
      data: PurchaseOrderDetailViewDto & { expectedArrivalDate?: string };
    }>(`/purchase-orders/${poId}`);

    const d = res.data?.data;
    if (!d) {
      throw new Error(`Không tìm thấy thông tin đơn mua hàng #${poId}.`);
    }

    return {
      ...d,
      issueDate: String(d.issueDate || "").slice(0, 10),
      promisedEtaDate: String(d.promisedEtaDate || d.expectedArrivalDate || "").slice(0, 10),
      lines: Array.isArray(d.lines) ? d.lines : [],
    };
  },

  /** UC-26: Lấy danh sách nhà cung cấp đang hoạt động (ACTIVE) để chọn gửi đơn đặt hàng */
  getActiveSuppliers: async (): Promise<ActiveSupplierOptionDto[]> => {
    const res = await apiClient.get<{
      success: boolean;
      data:
        | ActiveSupplierOptionDto[]
        | { items?: ActiveSupplierOptionDto[] };
    }>("/suppliers", { params: { status: "ACTIVE", pageSize: 100 } });

    const payload = res.data?.data;
    const list = Array.isArray(payload)
      ? payload
      : Array.isArray(payload?.items)
        ? payload.items
        : [];

    return list.filter(
      (s) => String(s.status || "ACTIVE").toUpperCase() === "ACTIVE",
    );
  },

  /** UC-49: Lấy danh sách nguyên vật liệu cần mua từ các Yêu cầu mua hàng đã được Chủ xưởng duyệt */
  getApprovedRequisitions: async (): Promise<ApprovedRequisitionViewDto[]> => {
    const res = await apiClient.get<{
      success: boolean;
      data: ApprovedRequisitionViewDto[];
    }>("/purchase-orders/approved-requisitions");

    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  /** UC-52: Phân phối danh sách nguyên liệu cần mua cho các nhà cung cấp đã chọn và tách thành các đơn đặt hàng tương ứng */
  distributeMaterialsToSuppliers: async (payload: {
    promisedEtaDate?: string;
    terms?: string;
    lines: DistributeMaterialLinePayloadDto[];
  }): Promise<SupplierPurchaseOrderDto[]> => {
    const realLines = payload.lines.filter((l) => l.prId > 0);
    const uniqueSupplierIds = Array.from(
      new Set(realLines.map((l) => l.supplierId)),
    );
    const res = await apiClient.post<{
      success: boolean;
      data: SupplierPurchaseOrderDto[];
    }>("/purchase-orders/batch-issue", {
      autoSendEmail: true,
      lines: realLines.map((l) => ({
        prId: l.prId,
        prItemId: l.prItemId,
        supplierId: l.supplierId,
        orderedQty: l.orderedQty,
        unitPrice: l.unitPrice,
        expectedDeliveryDate: l.expectedArrivalDate,
      })),
      supplierTerms: uniqueSupplierIds.map((supplierId) => {
        const fallbackLineEta = realLines
          .filter((l) => l.supplierId === supplierId && l.expectedArrivalDate)
          .map((l) => l.expectedArrivalDate as string)
          .sort()[0];
        return {
          supplierId,
          expectedArrivalDate:
            payload.promisedEtaDate ||
            fallbackLineEta ||
            new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10),
          terms:
            payload.terms ||
            "Thanh toán trước 50% giá trị hợp đồng, 50% còn lại thanh toán sau khi giao hàng.",
        };
      }),
    });

    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  /** UC-52: Gửi email Đơn đặt hàng (ĐƠN ĐẶT HÀNG theo mẫu hành chính Việt Nam) tới nhà cung cấp */
  sendPurchaseOrderEmail: async (
    poId: number,
    payload: SendPoEmailRequestDto,
  ): Promise<{ message: string; data: SendPoEmailResponseDto }> => {
    const res = await apiClient.post<{
      success: boolean;
      message: string;
      data: SendPoEmailResponseDto;
    }>(`/purchase-orders/${poId}/send-email`, payload);

    return {
      message:
        res.data?.message ||
        `Đã gửi đơn đặt hàng tới nhà cung cấp (${res.data?.data?.recipientEmail || payload.recipientEmail}) thành công.`,
      data: res.data?.data,
    };
  },

  /** UC-52: Tạo đơn đặt hàng nhà cung cấp từ PR đã duyệt (POST /api/v1/purchase-orders) */
  createPurchaseOrder: async (payload: CreateSupplierPoDto): Promise<SupplierPurchaseOrderDto> => {
    const res = await apiClient.post<{
      success: boolean;
      data: SupplierPurchaseOrderDto;
    }>("/purchase-orders", payload);

    return res.data.data;
  },

  /** Phát hành đồng loạt các đơn mua hàng đã được Owner phê duyệt (Batch Issue POs) */
  batchIssuePurchaseOrders: async (): Promise<SupplierPurchaseOrderDto[]> => {
    const res = await apiClient.post<{
      success: boolean;
      data: SupplierPurchaseOrderDto[];
    }>("/purchase-orders/batch-issue");

    return Array.isArray(res.data?.data) ? res.data.data : [];
  },

  /** UC-53: Hủy đơn đặt hàng trước khi giao (chỉ áp dụng khi đơn chưa nhận hàng) */
  cancelPurchaseOrder: async (
    poId: number,
    reason?: string,
  ): Promise<{ message: string; data: SupplierPurchaseOrderDto }> => {
    const res = await apiClient.put<{
      success: boolean;
      message: string;
      data: SupplierPurchaseOrderDto;
    }>(`/purchase-orders/${poId}/status`, {
      status: "CANCELLED",
      reason: reason?.trim() || "Hủy đơn đặt hàng trước khi giao theo yêu cầu bộ phận Thu mua.",
    });

    return {
      message: res.data?.message || "Đã hủy đơn đặt hàng thành công.",
      data: res.data?.data,
    };
  },

  /** UC-53: Cập nhật lịch giao hàng dự kiến (Promised ETA Date) */
  updatePoEta: async (payload: UpdatePoEtaDto): Promise<SupplierPurchaseOrderDto | null> => {
    const res = await apiClient.put<{
      success: boolean;
      data: SupplierPurchaseOrderDto;
    }>(`/purchase-orders/${payload.poId}/eta`, payload);

    return res.data?.data ?? null;
  },
};
