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
    supplierName: "Hoang Thong Timber Ltd",
    materialCategory: "Solid Lumber",
    orderItemsSummary: "7.40 m³ European White Ash",
    issueDate: "2026-10-20",
    promisedEtaDate: "2026-10-23",
    etaDaysLabel: "3 Days",
    fulfillmentStatus: "PO ISSUED / IN TRANSIT",
    sourcePrCode: "Daily PR-2026-10-20",
  },
  {
    poId: 43,
    poCode: "PO-MAT-2026-043",
    supplierName: "Viet Duc Fasteners Co",
    materialCategory: "Hardware",
    orderItemsSummary: "300 Sets Corner Brackets + 120 Sets Hinges",
    issueDate: "2026-10-20",
    promisedEtaDate: "2026-10-22",
    etaDaysLabel: "2 Days",
    fulfillmentStatus: "PO ISSUED / IN TRANSIT",
    sourcePrCode: "Daily PR-2026-10-20",
  },
  {
    poId: 44,
    poCode: "PO-MAT-2026-044",
    supplierName: "Duy Tien Packaging Co",
    materialCategory: "Export Boxes",
    orderItemsSummary: "600 Sets Bed Frame Master Cartons",
    issueDate: "2026-10-20",
    promisedEtaDate: "2026-10-24",
    etaDaysLabel: "4 Days",
    fulfillmentStatus: "SUPPLIER CONFIRMED",
    sourcePrCode: "Daily PR-2026-10-20",
  },
  {
    poId: 39,
    poCode: "PO-MAT-2026-039",
    supplierName: "Oseven Coatings VN",
    materialCategory: "Finishing PU",
    orderItemsSummary: "250 Kg PU Primer & Topcoat",
    issueDate: "2026-10-14",
    promisedEtaDate: "2026-10-18",
    etaDaysLabel: null,
    fulfillmentStatus: "RECEIVED & COMMITTED",
    sourcePrCode: "Daily PR-2026-10-14",
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
        data: SupplierPurchaseOrderDto[];
      }>("/purchase-orders", { params });
      if (res.data?.data && res.data.data.length > 0) {
        return res.data.data;
      }
    } catch {
      // Fallback sang dữ liệu chuẩn trong bộ nhớ khi Backend chưa mở endpoint riêng
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
    } catch {
      // Xử lý local state nếu chạy độc lập
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
      etaDaysLabel: `${diffDays} Days`,
      fulfillmentStatus: "SUPPLIER CONFIRMED",
      sourcePrCode: payload.sourcePrCode || "Daily PR-2026-10-20",
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
    } catch {
      // Xử lý local state
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
    } catch {
      // Xử lý local state
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
            : `${days} Days`,
        fulfillmentStatus: payload.fulfillmentStatus || po.fulfillmentStatus,
      };
      return updatedRecord;
    });

    return updatedRecord;
  },
};

