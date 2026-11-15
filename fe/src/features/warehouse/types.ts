/** Kiểu dữ liệu nghiệp vụ kho: nhập kho theo PO và xuất kho cấp phát. */

export interface OpenPurchaseOrderLine {
  materialId: number;
  materialCode: string;
  materialName: string;
  unitOfMeasure: string;
  orderedQty: number;
  receivedQty: number;
  remainingQty: number;
}

export interface OpenPurchaseOrder {
  poId: number;
  poCode: string;
  supplierName: string;
  poStatus: string;
  issueDate: string;
  expectedArrivalDate: string;
  lines: OpenPurchaseOrderLine[];
}

export interface CommitGoodsReceiptPayload {
  poId: number;
  lines: { materialId: number; receivedQty: number }[];
  note?: string;
}

export interface GoodsReceiptLineResult {
  materialId: number;
  materialCode: string;
  materialName: string;
  unitOfMeasure: string;
  orderedQty: number;
  previouslyReceivedQty: number;
  receivedQty: number;
  totalReceivedQty: number;
  remainingQty: number;
  physicalQtyAfter: number;
  allocatedQtyAfter: number;
  availableQtyAfter: number;
}

export interface AutoAllocationResult {
  allocationId: number;
  orderId: number;
  poNumber: string;
  spaceKey: string;
  etdDate: string;
  orderItemId: number;
  materialId: number;
  materialCode: string;
  allocatedNowQty: number;
  requiredQty: number;
  reservedQty: number;
  shortageQty: number;
}

export interface GoodsReceiptResult {
  grnId: number;
  grnCode: string;
  poId: number;
  poCode: string;
  poStatus: string;
  grnStatus: string;
  committedAt: string;
  lines: GoodsReceiptLineResult[];
  allocations: AutoAllocationResult[];
  allocatedOrderCount: number;
  readyOrderPoNumbers: string[];
}

/** Đơn hàng bán rút gọn dùng để chọn đơn khi xuất kho. */
export interface IssueOrderOption {
  orderId: number;
  poNumber: string;
  orderSpaceKey: string;
  customerName: string;
  etdDate: string;
  orderStatus: string;
}

/** Bản ghi cấp phát (giữ chỗ) vật tư của một đơn hàng. */
export interface OrderAllocation {
  allocationId: number;
  orderId: number;
  poNumber: string;
  spaceKey: string;
  customerName: string;
  orderItemId: number;
  productSku: string;
  productName: string;
  requiredQty: number;
  reservedQty: number;
  issuedQty: number;
  shortageQty: number;
  allocationStatus: string;
  etdDate: string;
  allocatedAt: string;
}
