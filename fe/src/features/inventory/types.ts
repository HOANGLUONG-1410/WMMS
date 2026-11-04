export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageIndex?: number;
  pageSize: number;
  totalPages?: number;
  hasPreviousPage?: boolean;
  hasNextPage?: boolean;
}

export interface InventoryStockFilterDto {
  search?: string;
  category?: string;
  group?: string;
  belowSafetyStock?: boolean;
  page?: number;
  pageSize?: number;
}

export interface MaterialOrderAllocationDto {
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

export interface InventoryStockDto {
  stockId: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  materialGroup: string;
  unitOfMeasure: string;
  dimensionSpec?: string | null;
  fscCertified: boolean;
  safetyStockQty: number;
  physicalQty: number;
  allocatedQty: number;
  availableQty: number;
  isBelowSafetyStock: boolean;
  status: string;
  updatedAt: string;
  /** Danh sách tóm tắt các Order Space đang giữ chỗ để hiển thị pill trên bảng 3 cấp */
  activeAllocationsPreview?: Array<{
    spaceKey: string;
    poNumber?: string;
    reservedQty: number;
  }>;
}

export interface InventoryTransactionDto {
  transactionId: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  materialGroup: string;
  unitOfMeasure: string;
  txType: "INITIAL_IMPORT" | "IN_RECEIPT" | "OUT_ISSUE" | "ADJUST" | string;
  quantity: number;
  referenceCode?: string | null;
  createdAt: string;
}

export interface InventoryStockDetailDto extends InventoryStockDto {
  allocations: MaterialOrderAllocationDto[];
  recentTransactions: InventoryTransactionDto[];
}

export interface InventoryLedgerFilterDto {
  materialId?: number;
  txType?: string;
  search?: string;
  startDate?: string;
  endDate?: string;
  page?: number;
  pageSize?: number;
}

