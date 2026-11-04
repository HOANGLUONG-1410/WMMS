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

