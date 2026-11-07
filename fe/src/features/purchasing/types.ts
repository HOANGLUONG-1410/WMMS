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
