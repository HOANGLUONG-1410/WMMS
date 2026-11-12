export type PoFulfillmentStatus =
  | "PO ISSUED / IN TRANSIT"
  | "SUPPLIER CONFIRMED"
  | "RECEIVED & COMMITTED"
  | string;

export interface SupplierPurchaseOrderDto {
  poId: number;
  poCode: string;
  supplierName: string;
  materialCategory: string;
  orderItemsSummary: string;
  issueDate: string;
  promisedEtaDate: string;
  etaDaysLabel?: string | null;
  fulfillmentStatus: PoFulfillmentStatus;
  sourcePrCode?: string;
}

export interface CreateSupplierPoDto {
  supplierName: string;
  materialCategory: string;
  orderItemsSummary: string;
  promisedEtaDate: string;
  sourcePrCode?: string;
}

export interface UpdatePoEtaDto {
  poId: number;
  promisedEtaDate: string;
  fulfillmentStatus?: PoFulfillmentStatus;
}

export interface PurchaseOrderQueryParams {
  searchKeyword?: string;
  status?: string;
}

/** Mã trạng thái phiếu yêu cầu mua hàng (PR) do backend trả về. */
export type PrStatus = "PENDING_APPROVAL" | "APPROVED" | "REJECTED" | "PO_ISSUED";

export interface PurchaseRequestListItem {
  prId: number;
  prCode: string;
  aggregationDate: string;
  prStatus: PrStatus | string;
  itemsCount: number;
  totalShortageQty: number;
  totalSuggestedOrderQty: number;
  createdBy?: number | null;
  creatorName?: string | null;
  approvedBy?: number | null;
  approverName?: string | null;
  reviewedAt?: string | null;
  createdAt: string;
}

export interface PurchaseRequestSourceOrder {
  orderId: number;
  poNumber: string;
  spaceKey?: string | null;
  orderItemId: number;
  productSku?: string | null;
  etdDate?: string | null;
  shortageQty: number;
}

export interface PurchaseRequestItem {
  prItemId: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  unitOfMeasure?: string | null;
  totalShortageQty: number;
  suggestedOrderQty: number;
  availableStockQty: number;
  earliestEtd?: string | null;
  sourceOrders: PurchaseRequestSourceOrder[];
}

export interface PurchaseRequestDetail extends PurchaseRequestListItem {
  rejectionReason?: string | null;
  purchaseOrdersCount: number;
  contributingSpaceKeys: string[];
  items: PurchaseRequestItem[];
}

export interface PurchaseRequestQueryParams {
  status?: string;
  fromDate?: string;
  toDate?: string;
  searchKeyword?: string;
  pageNumber?: number;
  pageSize?: number;
}

/** Lớp màu huy hiệu theo trạng thái PR (chỉ dùng hiển thị). */
export const prStatusBadgeClass = (status: string): string => {
  switch (String(status).toUpperCase()) {
    case "PENDING_APPROVAL":
      return "bg-amber-50 text-amber-800 border-amber-200";
    case "APPROVED":
      return "bg-emerald-50 text-emerald-700 border-emerald-200";
    case "REJECTED":
      return "bg-rose-50 text-rose-700 border-rose-200";
    case "PO_ISSUED":
      return "bg-blue-50 text-blue-700 border-blue-200";
    default:
      return "bg-slate-100 text-slate-700 border-slate-200";
  }
};

/** Nhãn tiếng Việt hiển thị cho trạng thái thực hiện PO (giá trị gốc vẫn giữ nguyên để so sánh logic). */
export const fulfillmentStatusLabel = (status: PoFulfillmentStatus): string => {
  switch (status) {
    case "PO ISSUED / IN TRANSIT":
      return "ĐÃ PHÁT HÀNH PO / ĐANG VẬN CHUYỂN";
    case "SUPPLIER CONFIRMED":
      return "NCC ĐÃ XÁC NHẬN";
    case "RECEIVED & COMMITTED":
      return "ĐÃ NHẬN & XÁC NHẬN NHẬP KHO";
    default:
      return String(status);
  }
};
