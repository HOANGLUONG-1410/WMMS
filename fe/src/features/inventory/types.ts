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

// =============================================================================
// DTO phân hệ Tính toán Nhu cầu Nguyên vật liệu (MRP) & Phân bổ Tồn kho Tự động (SC-14)
// =============================================================================

export interface MrpOrderItemBreakdownDto {
  orderId: number;
  poNumber: string;
  spaceId?: number;
  spaceKey: string;
  orderItemId: number;
  productId: number;
  productSku: string;
  productName: string;
  orderedQty: number;
  scrapRatePercent: number;
  netQtyPerUnit: number;
  grossDemandQty: number;
  reservedQty: number;
  issuedQty: number;
  unfulfilledDemandQty: number;
  etdDate: string;
  orderItemStatus: string;
}

export interface MrpMaterialRequirementLineDto {
  materialId: number;
  materialCode: string;
  materialName: string;
  materialGroup: string;
  unitOfMeasure: string;
  dimensionSpec?: string | null;
  fscCertified: boolean;
  safetyStockQty: number;
  totalNetBomQty: number;
  totalGrossDemandQty: number;
  physicalStockQty: number;
  allocatedToOtherOrdersQty: number;
  alreadyReservedForTargetQty: number;
  availableStockQty: number;
  allocatableNowQty: number;
  netShortageQty: number;
  suggestedPrOrderQty: number;
  isShortage: boolean;
  supplyStatus: "SUFFICIENT" | "PARTIAL_SHORTAGE" | "CRITICAL_SHORTAGE" | string;
  itemBreakdowns: MrpOrderItemBreakdownDto[];
}

export interface MrpAnalysisResultDto {
  scope: "ORDER" | "ORDER_SPACE" | "ORDER_ITEM" | "FACTORY_ALL" | string;
  orderId?: number;
  poNumber?: string;
  spaceId?: number;
  spaceKey?: string;
  customerName?: string;
  etdDate?: string;
  isEtdAtRisk: boolean;
  analyzedAt: string;
  totalOrderItemsCount: number;
  readyOrderItemsCount: number;
  waitingMaterialItemsCount: number;
  totalDistinctMaterialsCount: number;
  sufficientMaterialsCount: number;
  shortageMaterialsCount: number;
  overallReadinessStatus: "READY_TO_PRODUCE" | "WAITING_FOR_MATERIAL" | string;
  materialRequirements: MrpMaterialRequirementLineDto[];
}

export interface AutoAllocationRequestDto {
  autoQueueShortageToDailyPr?: boolean;
  notes?: string;
}

export interface AutoAllocationResultDto {
  orderId?: number;
  poNumber?: string;
  spaceId?: number;
  spaceKey?: string;
  executedAt: string;
  overallReadinessStatus: string;
  totalOrderItemsProcessed: number;
  fullyAllocatedItemsCount: number;
  waitingMaterialItemsCount: number;
  totalMaterialAllocationsUpdated: number;
  consolidatedPrId?: number;
  consolidatedPrCode?: string;
  queuedShortageMaterialsCount: number;
  allocations: MaterialOrderAllocationDto[];
  remainingShortages: MrpMaterialRequirementLineDto[];
}

export interface ReallocateByEtdRequestDto {
  materialIds?: number[];
  referenceCode?: string;
}

export interface ReallocateByEtdResultDto {
  executedAt: string;
  referenceCode?: string;
  materialsReallocatedCount: number;
  allocationsIncreasedCount: number;
  orderItemsUnlockedToReadyCount: number;
  unlockedOrderItemIds: number[];
  updatedAllocations: MaterialOrderAllocationDto[];
}

export interface ReleaseOrderAllocationsRequestDto {
  reason?: string;
}


