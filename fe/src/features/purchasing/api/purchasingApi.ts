import { apiClient } from "../../../lib/axios";
import type {
  CreateSupplierPoDto,
  PurchaseOrderQueryParams,
  SupplierPurchaseOrderDto,
  UpdatePoEtaDto,
} from "../types";

export const INITIAL_SUPPLIER_POS: SupplierPurchaseOrderDto[] = [
  {
    poId: 42,
    poCode: "PO-MAT-2026-042",
    supplierName: "Công ty gỗ Hoàng Thông",
    materialCategory: "Gỗ nguyên khối",
    orderItemsSummary: "7,40 m³ gỗ tần bì trắng châu Âu",
    issueDate: "2026-10-20",
    promisedEtaDate: "2026-10-23",
    etaDaysLabel: "3 ngày",
    fulfillmentStatus: "PO ISSUED / IN TRANSIT",
    sourcePrCode: "PR gộp ngày 2026-10-20",
  },
  {
    poId: 43,
    poCode: "PO-MAT-2026-043",
    supplierName: "Công ty phụ kiện Việt Đức",
    materialCategory: "Phụ kiện",
    orderItemsSummary: "300 bộ ke góc + 120 bộ bản lề",
    issueDate: "2026-10-20",
    promisedEtaDate: "2026-10-22",
    etaDaysLabel: "2 ngày",
    fulfillmentStatus: "PO ISSUED / IN TRANSIT",
    sourcePrCode: "PR gộp ngày 2026-10-20",
  },
  {
    poId: 44,
    poCode: "PO-MAT-2026-044",
    supplierName: "Công ty bao bì Duy Tiên",
    materialCategory: "Thùng xuất khẩu",
    orderItemsSummary: "600 bộ thùng carton khung giường",
    issueDate: "2026-10-20",
    promisedEtaDate: "2026-10-24",
    etaDaysLabel: "4 ngày",
    fulfillmentStatus: "SUPPLIER CONFIRMED",
    sourcePrCode: "PR gộp ngày 2026-10-20",
  },
  {
    poId: 39,
    poCode: "PO-MAT-2026-039",
    supplierName: "Oseven Coatings VN",
    materialCategory: "Sơn PU hoàn thiện",
    orderItemsSummary: "250 kg sơn lót & sơn phủ PU",
    issueDate: "2026-10-14",
    promisedEtaDate: "2026-10-18",
    etaDaysLabel: null,
    fulfillmentStatus: "RECEIVED & COMMITTED",
    sourcePrCode: "PR gộp ngày 2026-10-14",
  },
];

export interface PoLineItemDto {
  poDetailId: number;
  materialId: number;
  materialCode: string;
  materialName: string;
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

const FALLBACK_ACTIVE_SUPPLIERS: ActiveSupplierOptionDto[] = [
  {
    supplierId: 1,
    supplierCode: "SUP-WOOD-LAMVIET",
    supplierName: "Công ty CP Chế biến Gỗ Lâm Việt",
    supplyCategory: "LUMBER",
    contactEmail: "sales@lamvietwood.vn",
    status: "ACTIVE",
  },
  {
    supplierId: 2,
    supplierCode: "SUP-WOOD-TAVICO",
    supplierName: "Công ty CP Gỗ Tây Ngô Tavico Timber",
    supplyCategory: "LUMBER",
    contactEmail: "info@tavicowood.com",
    status: "ACTIVE",
  },
  {
    supplierId: 3,
    supplierCode: "SUP-HARDWARE-HAFELE",
    supplierName: "Công ty TNHH Hafele Việt Nam",
    supplyCategory: "HARDWARE",
    contactEmail: "support.vn@hafele.com",
    status: "ACTIVE",
  },
  {
    supplierId: 4,
    supplierCode: "SUP-HARDWARE-BLUM",
    supplierName: "Công ty TNHH Phụ kiện Mộc Blum",
    supplyCategory: "HARDWARE",
    contactEmail: "sales@blum.vn",
    status: "ACTIVE",
  },
  {
    supplierId: 5,
    supplierCode: "SUP-PAINT-OSEVEN",
    supplierName: "Công ty Cổ phần Sơn Oseven PU",
    supplyCategory: "FINISH_PAINT",
    contactEmail: "kinhdoanh@osevenpaint.com",
    status: "ACTIVE",
  },
  {
    supplierId: 6,
    supplierCode: "SUP-PACK-THANGLONG",
    supplierName: "Bao bì & Thùng Carton Thăng Long",
    supplyCategory: "PACKAGING",
    contactEmail: "sales@thanglongbox.com.vn",
    status: "ACTIVE",
  },
];

let inMemoryPurchaseOrders: SupplierPurchaseOrderDto[] = [...INITIAL_SUPPLIER_POS];

export const purchasingApi = {
  /** UC-50: Truy vấn danh sách đơn mua hàng nhà cung cấp (GET /api/v1/purchase-orders) */
  getPurchaseOrders: async (
    params: PurchaseOrderQueryParams = {},
  ): Promise<SupplierPurchaseOrderDto[]> => {
    try {
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

      if (rawItems.length > 0) {
        return rawItems.map((po) => ({
          ...po,
          issueDate: String(po.issueDate || "").slice(0, 10),
          promisedEtaDate: String(
            po.promisedEtaDate ||
              (po as { expectedArrivalDate?: string }).expectedArrivalDate ||
              "",
          ).slice(0, 10),
        }));
      }
    } catch (err: any) {
      if (err?.status && err.status !== 0 && err.status !== 404) {
        throw err;
      }
      // Fallback sang dữ liệu chuẩn trong bộ nhớ khi Backend offline hoặc chưa có endpoint
    }

    const kw = (params.searchKeyword || "").trim().toLowerCase();
    if (!kw) return [...inMemoryPurchaseOrders];

    return inMemoryPurchaseOrders.filter(
      (po) =>
        po.poCode.toLowerCase().includes(kw) ||
        po.supplierName.toLowerCase().includes(kw) ||
        po.materialCategory.toLowerCase().includes(kw) ||
        po.orderItemsSummary.toLowerCase().includes(kw),
    );
  },

  /** UC-51: Xem chi tiết đơn đặt hàng kèm danh sách mặt hàng nguyên vật liệu (GET /api/v1/purchase-orders/:id) */
  getPurchaseOrderById: async (
    poId: number,
    fallbackPo?: SupplierPurchaseOrderDto | null,
  ): Promise<PurchaseOrderDetailViewDto> => {
    try {
      const res = await apiClient.get<{
        success: boolean;
        data: PurchaseOrderDetailViewDto & { expectedArrivalDate?: string };
      }>(`/purchase-orders/${poId}`);
      if (res.data?.data) {
        const d = res.data.data;
        return {
          ...d,
          issueDate: String(d.issueDate || "").slice(0, 10),
          promisedEtaDate: String(d.promisedEtaDate || d.expectedArrivalDate || "").slice(0, 10),
          lines: Array.isArray(d.lines) ? d.lines : [],
        };
      }
    } catch {
      // Fallback nếu là đơn mẫu trong bộ nhớ
    }

    const base =
      fallbackPo ||
      inMemoryPurchaseOrders.find((p) => p.poId === poId) ||
      INITIAL_SUPPLIER_POS[0];

    return {
      ...base,
      supplierId: 1,
      supplierEmail: "sales@lamvietwood.vn",
      terms: "Thanh toán trước 50% giá trị hợp đồng, 50% còn lại thanh toán sau khi giao hàng.",
      totalAmount: 92500000,
      lines: [
        {
          poDetailId: 1,
          materialId: 1,
          materialCode: "WOOD_OAK_01",
          materialName: base.orderItemsSummary || "Gỗ sồi Bắc Mỹ xẻ sấy FAS",
          unitOfMeasure: "M3",
          orderedQty: 7.4,
          receivedQty: 0,
          remainingQty: 7.4,
          unitPrice: 12500000,
          lineAmount: 92500000,
        },
      ],
    };
  },

  /** UC-26: Lấy danh sách nhà cung cấp đang hoạt động (ACTIVE) để chọn gửi đơn đặt hàng */
  getActiveSuppliers: async (): Promise<ActiveSupplierOptionDto[]> => {
    try {
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

      const activeList = list.filter(
        (s) => String(s.status || "ACTIVE").toUpperCase() === "ACTIVE",
      );
      if (activeList.length > 0) {
        return activeList;
      }
    } catch {
      // Fallback sang danh sách nhà cung cấp mặc định
    }
    return FALLBACK_ACTIVE_SUPPLIERS;
  },

  /** UC-49: Lấy danh sách nguyên vật liệu cần mua từ các Yêu cầu mua hàng đã được Chủ xưởng duyệt */
  getApprovedRequisitions: async (): Promise<ApprovedRequisitionViewDto[]> => {
    try {
      const res = await apiClient.get<{
        success: boolean;
        data: ApprovedRequisitionViewDto[];
      }>("/purchase-orders/approved-requisitions");

      if (Array.isArray(res.data?.data) && res.data.data.length > 0) {
        return res.data.data;
      }
    } catch {
      // Fallback nếu chưa có PR ở trạng thái APPROVED
    }

    return [
      {
        prId: 0,
        prCode: "PR-20261121-001",
        aggregationDate: new Date().toISOString().slice(0, 10),
        lines: [
          {
            prItemId: 101,
            materialId: 1,
            materialCode: "WOOD_OAK_01",
            materialName: "Gỗ sồi Bắc Mỹ xẻ sấy FAS (Oak Timber)",
            materialGroup: "LUMBER",
            unitOfMeasure: "M3",
            suggestedOrderQty: 7.4,
            orderedQty: 0,
            remainingQty: 7.4,
          },
          {
            prItemId: 102,
            materialId: 2,
            materialCode: "HW_HINGE_02",
            materialName: "Bản lề giảm chấn & Ke góc thép chịu lực",
            materialGroup: "HARDWARE",
            unitOfMeasure: "BO",
            suggestedOrderQty: 300,
            orderedQty: 0,
            remainingQty: 300,
          },
          {
            prItemId: 103,
            materialId: 3,
            materialCode: "PAINT_PU_03",
            materialName: "Sơn lót & Sơn phủ PU 2 thành phần",
            materialGroup: "FINISH_PAINT",
            unitOfMeasure: "KG",
            suggestedOrderQty: 150,
            orderedQty: 0,
            remainingQty: 150,
          },
          {
            prItemId: 104,
            materialId: 4,
            materialCode: "PACK_CARTON_04",
            materialName: "Thùng Carton 5 lớp sóng BC đóng gói xuất khẩu",
            materialGroup: "PACKAGING",
            unitOfMeasure: "THUNG",
            suggestedOrderQty: 600,
            orderedQty: 0,
            remainingQty: 600,
          },
        ],
      },
    ];
  },

  /** UC-52: Phân phối danh sách nguyên liệu cần mua cho các nhà cung cấp đã chọn và tách thành các đơn đặt hàng tương ứng */
  distributeMaterialsToSuppliers: async (payload: {
    promisedEtaDate: string;
    terms?: string;
    lines: DistributeMaterialLinePayloadDto[];
  }): Promise<SupplierPurchaseOrderDto[]> => {
    const realLines = payload.lines.filter((l) => l.prId > 0);
    if (realLines.length > 0) {
      const uniqueSupplierIds = Array.from(
        new Set(realLines.map((l) => l.supplierId)),
      );
      const res = await apiClient.post<{
        success: boolean;
        data: SupplierPurchaseOrderDto[];
      }>("/purchase-orders/batch-issue", {
        lines: realLines.map((l) => ({
          prId: l.prId,
          prItemId: l.prItemId,
          supplierId: l.supplierId,
          orderedQty: l.orderedQty,
          unitPrice: l.unitPrice,
        })),
        supplierTerms: uniqueSupplierIds.map((supplierId) => ({
          supplierId,
          expectedArrivalDate: payload.promisedEtaDate,
          terms:
            payload.terms ||
            "Thanh toán trước 50% giá trị hợp đồng, 50% còn lại thanh toán sau khi giao hàng.",
        })),
      });

      if (Array.isArray(res.data?.data)) {
        return res.data.data;
      }
    }

    // Trường hợp tạo từ danh sách mẫu khi mọi PR trong DB đã phát hành hết
    const suppliers = await purchasingApi.getActiveSuppliers();
    const grouped = new Map<number, DistributeMaterialLinePayloadDto[]>();
    for (const line of payload.lines) {
      const arr = grouped.get(line.supplierId) || [];
      arr.push(line);
      grouped.set(line.supplierId, arr);
    }

    const createdList: SupplierPurchaseOrderDto[] = [];
    for (const [supplierId, groupLines] of grouped.entries()) {
      const sup = suppliers.find((s) => s.supplierId === supplierId);
      const summary = groupLines
        .map(
          (g) =>
            `${g.orderedQty} ${g.unitOfMeasure || ""} ${g.materialName || ""}`.trim(),
        )
        .join(" + ");
      const created = await purchasingApi.createPurchaseOrder({
        supplierName: sup?.supplierName || `Nhà cung cấp #${supplierId}`,
        materialCategory: groupLines[0]?.materialGroup || "Vật tư sản xuất",
        orderItemsSummary: summary,
        promisedEtaDate: payload.promisedEtaDate,
        sourcePrCode: "PR-20261121-001",
      });
      createdList.push(created);
    }

    return createdList;
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
    try {
      const res = await apiClient.post<{
        success: boolean;
        data: SupplierPurchaseOrderDto;
      }>("/purchase-orders", payload);
      if (res.data?.data) {
        return res.data.data;
      }
    } catch (err: any) {
      if (err?.status && err.status !== 0 && err.status !== 404) {
        throw err;
      }
      // Xử lý local state nếu chạy offline hoặc endpoint chưa cấu hình
    }

    const nextNum = 45 + inMemoryPurchaseOrders.length - 4;
    const todayStr = new Date().toISOString().slice(0, 10);
    const diffDays = Math.max(
      1,
      Math.ceil(
        (new Date(payload.promisedEtaDate).getTime() - new Date(todayStr).getTime()) /
          (1000 * 60 * 60 * 24),
      ),
    );

    const created: SupplierPurchaseOrderDto = {
      poId: Date.now(),
      poCode: `PO-MAT-2026-0${nextNum}`,
      supplierName: payload.supplierName,
      materialCategory: payload.materialCategory,
      orderItemsSummary: payload.orderItemsSummary,
      issueDate: todayStr,
      promisedEtaDate: payload.promisedEtaDate,
      etaDaysLabel: `${diffDays} ngày`,
      fulfillmentStatus: "SUPPLIER CONFIRMED",
      sourcePrCode: payload.sourcePrCode || "PR gộp ngày 2026-10-20",
    };

    inMemoryPurchaseOrders = [created, ...inMemoryPurchaseOrders];
    return created;
  },

  /** Phát hành đồng loạt các đơn mua hàng đã được Owner phê duyệt (Batch Issue POs) */
  batchIssuePurchaseOrders: async (): Promise<SupplierPurchaseOrderDto[]> => {
    try {
      const res = await apiClient.post<{
        success: boolean;
        data: SupplierPurchaseOrderDto[];
      }>("/purchase-orders/batch-issue");
      if (res.data?.data) {
        return res.data.data;
      }
    } catch (err: any) {
      if (err?.status && err.status !== 0 && err.status !== 404) {
        throw err;
      }
      // Xử lý local state khi offline
    }

    inMemoryPurchaseOrders = inMemoryPurchaseOrders.map((po) =>
      po.fulfillmentStatus === "SUPPLIER CONFIRMED"
        ? { ...po, fulfillmentStatus: "PO ISSUED / IN TRANSIT" }
        : po,
    );
    return [...inMemoryPurchaseOrders];
  },

  /** UC-53: Cập nhật lịch giao hàng dự kiến (Promised ETA Date) */
  updatePoEta: async (payload: UpdatePoEtaDto): Promise<SupplierPurchaseOrderDto | null> => {
    try {
      const res = await apiClient.put<{
        success: boolean;
        data: SupplierPurchaseOrderDto;
      }>(`/purchase-orders/${payload.poId}/eta`, payload);
      if (res.data?.data) {
        return res.data.data;
      }
    } catch (err: any) {
      if (err?.status && err.status !== 0 && err.status !== 404) {
        throw err;
      }
      // Xử lý local state khi offline
    }

    let updatedRecord: SupplierPurchaseOrderDto | null = null;
    inMemoryPurchaseOrders = inMemoryPurchaseOrders.map((po) => {
      if (po.poId !== payload.poId) return po;
      const issueTime = new Date(po.issueDate).getTime();
      const etaTime = new Date(payload.promisedEtaDate).getTime();
      const days = Math.max(1, Math.ceil((etaTime - issueTime) / (1000 * 60 * 60 * 24)));
      updatedRecord = {
        ...po,
        promisedEtaDate: payload.promisedEtaDate,
        etaDaysLabel:
          (payload.fulfillmentStatus || po.fulfillmentStatus) === "RECEIVED & COMMITTED"
            ? null
            : `${days} ngày`,
        fulfillmentStatus: payload.fulfillmentStatus || po.fulfillmentStatus,
      };
      return updatedRecord;
    });

    return updatedRecord;
  },
};

