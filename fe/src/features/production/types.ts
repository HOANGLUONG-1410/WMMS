// fe/src/features/production/types.ts

/**
 * Trạng thái của công đoạn thẻ lô (khớp 100% với StepStatusEnum ở Backend)
 */
export type BatchStepStatus =
  | "WAITING"
  | "IN_PROGRESS"
  | "QC_PENDING"
  | "PASSED"
  | "BLOCKED";

/**
 * Trạng thái của thẻ lô Kanban / Production Batch (khớp 100% với BatchStatusEnum ở Backend)
 */
export type ProductionBatchStatus =
  | "LOCKED"
  | "IN_PROGRESS"
  | "QC_PENDING"
  | "HOLD_POINT_BLOCKED"
  | "DONE";

/**
 * Loại thẻ lô sản xuất (khớp 100% với BatchTypeEnum ở Backend)
 */
export type ProductionBatchType =
  | "MAIN_CARD"
  | "REWORK_SUB_CARD"
  | "REPLENISH_LOT";

/**
 * DTO Thẻ Lô Sản xuất Kanban (SC-19, QT-05)
 */
export interface ProductionBatchDto {
  batchId: number;
  batchCode: string;
  orderItemId: number;
  parentBatchId?: number | null;
  batchType: ProductionBatchType | string;
  batchQty: number;
  currentStageSeq: number;
  currentStageName: string;
  currentWorkstationCode: string;
  isOverdriver: boolean;
  holdPointStageSeq?: number | null;
  goodsIssuedUnlocked: boolean;
  batchStatus: ProductionBatchStatus | string;
  batchStatusText?: string;
  createdAt: string;
  steps: BatchRoutingStepDto[];
}

/**
 * DTO Công đoạn thực thi của Thẻ Lô Sản xuất (Sequential State Machine - QT-05)
 */
export interface BatchRoutingStepDto {
  batchStepId: number;
  batchId: number;
  stageSequence: number;
  stageName: string;
  workstationCode: string;
  workstationName: string;
  qualityGateCode: string;
  qualityGateName: string;
  inspectionCriteria: string;
  isHoldPoint: boolean;
  targetQty: number;
  passedQty: number;
  defectQty: number;
  stepStatus: BatchStepStatus | string;
  stepStatusText?: string;
  completedAt?: string | null;
}

/**
 * DTO Yêu cầu chia lô sản xuất thông minh và phát hành lệnh sản xuất (SC-19, UC-62)
 */
export interface GenerateBatchesRequestDto {
  orderItemId: number;
  maxBatchSize?: number | null;
  customBatchSplits?: number[] | null;
  allowOverdrive?: boolean;
  justification?: string | null;
}

/**
 * DTO Kết quả phát hành Lệnh sản xuất tổng hợp (SC-19)
 */
export interface ProductionOrderSummaryDto {
  orderId: number;
  poNumber: string;
  spaceKey: string;
  orderItemId: number;
  productSku: string;
  productName: string;
  totalOrderedQty: number;
  releasedQty: number;
  totalBatchesCount: number;
  stationCapacity: number;
  isOverdriver: boolean;
  holdPointStageSeq?: number | null;
  goodsIssuedUnlocked: boolean;
  message: string;
  batches: ProductionBatchDto[];
}

/**
 * DTO Yêu cầu chuyển bước công đoạn tuần tự (S -> S + 1)
 */
export interface AdvanceStepRequestDto {
  targetStageSeq?: number | null;
  notes?: string | null;
}

/**
 * DTO Ghi nhận kết quả kiểm định KCS tại cổng QC Gate (SC-19, QT-06, KBN01)
 */
export interface RecordQcInspectionDto {
  batchStepId: number;
  inspectedQty: number;
  passQty: number;
  failQty: number;
  defectCode?: string | null;
  defectName?: string | null;
  disposition: "REWORK_SUB_CARD" | "SCRAP_REPLENISH" | string;
  notes?: string | null;
}

/**
 * DTO Kết quả sau khi ghi nhận kiểm định chất lượng QC Gate
 */
export interface QcInspectionResultDto {
  qcId: number;
  batchStepId: number;
  batchId: number;
  inspectedQty: number;
  passQty: number;
  failQty: number;
  isBranched: boolean;
  spawnedSubBatchId?: number | null;
  spawnedSubBatchCode?: string | null;
  message: string;
}

/**
 * DTO Yêu cầu chạy trước Overdrive do Tổ trưởng sản xuất khởi tạo (SC-20, UC-63, QT-06)
 */
export interface RequestOverdriveDto {
  holdPointStageSeq?: number | null;
  justification: string;
}

/**
 * DTO Phê duyệt hoặc từ chối yêu cầu Overdrive của Chủ xưởng (SC-20, KBN02)
 */
export interface ApproveOverdriveDto {
  approved: boolean;
  holdPointStageSeq?: number | null;
  notes?: string | null;
}

/**
 * DTO Kết quả thực hiện thao tác Overdrive / Mở khóa Hold Point
 */
export interface OverdriveOperationResultDto {
  batchId: number;
  batchCode: string;
  isOverdriver: boolean;
  holdPointStageSeq?: number | null;
  batchStatus: ProductionBatchStatus | string;
  message: string;
}

/**
 * DTO Yêu cầu nghiệm thu lại (Re-inspection) cho thẻ nhánh phụ 3% Rework / Scrap Replenish (SC-20b, KBN01)
 */
export interface ReinspectSubBatchDto {
  batchStepId: number;
  inspectedQty: number;
  passQty: number;
  failQty: number;
  notes?: string | null;
}

/**
 * DTO Kết quả nghiệm thu lại thẻ nhánh phụ
 */
export interface ReinspectSubBatchResultDto {
  qcId: number;
  subBatchId: number;
  subBatchCode: string;
  inspectedQty: number;
  passQty: number;
  failQty: number;
  isFullyPassed: boolean;
  subBatchStatus: ProductionBatchStatus | string;
  message: string;
}

/**
 * DTO Yêu cầu tái hòa nhập / Gộp lô (Merge Lot) thẻ nhánh phụ vào thẻ chính (SC-20b)
 */
export interface MergeSubBatchDto {
  reason?: string | null;
}

/**
 * DTO Kết quả gộp thẻ nhánh phụ vào thẻ chính
 */
export interface MergeSubBatchResultDto {
  subBatchId: number;
  subBatchCode: string;
  mainBatchId: number;
  mainBatchCode: string;
  mergedQty: number;
  mainBatchNewQty: number;
  currentStageSeq: number;
  mainBatchStatus: ProductionBatchStatus | string;
  subBatchStatus: ProductionBatchStatus | string;
  message: string;
}

/**
 * DTO Yêu cầu cho thẻ nhánh phụ chuyển tiếp độc lập không gộp lô (SC-20b)
 */
export interface ContinueSubBatchIndependentlyDto {
  notes?: string | null;
}

/**
 * DTO Kết quả chuyển tiếp độc lập của thẻ nhánh phụ
 */
export interface ContinueSubBatchResultDto {
  subBatchId: number;
  subBatchCode: string;
  nextStageSeq: number;
  nextStageName: string;
  batchQty: number;
  batchStatus: ProductionBatchStatus | string;
  message: string;
}

/**
 * DTO Yêu cầu hoàn tất công đoạn đóng gói cuối cùng và nhập kho thành phẩm (UC-63, UC-64)
 */
export interface CompleteFinishedBatchRequestDto {
  receivedQty?: number | null;
  warehouseLocation?: string | null;
  handoverNote?: string | null;
}

/**
 * DTO Kết quả xác nhận hoàn tất lô sản xuất và tự động đồng bộ tiến độ đơn hàng
 */
export interface FinishedGoodsReceiptResultDto {
  batchId: number;
  batchCode: string;
  batchType: string;
  batchStatus: ProductionBatchStatus | string;
  completedBatchQty: number;
  orderItemId: number;
  orderId: number;
  poNumber: string;
  productCode: string;
  productName: string;
  orderedQty: number;
  totalCompletedQtyForOrderItem: number;
  remainingQtyForOrderItem: number;
  orderItemCompletionPercent: number;
  orderItemStatus: string;
  salesOrderStatus: string;
  isOrderItemFullyCompleted: boolean;
  isSalesOrderFullyCompleted: boolean;
  warehouseLocation?: string | null;
  handoverNote?: string | null;
  completedAt: string;
  message: string;
}

/**
 * DTO Tiến độ công đoạn của từng thẻ/lô Kanban thuộc dòng sản phẩm (ProductionTrackingDtos)
 */
export interface BatchStageProgressDto {
  batchId: number;
  batchCode: string;
  batchType: string;
  batchStatus: ProductionBatchStatus | string;
  initialQty: number;
  currentQty: number;
  currentStageCode: string;
  passedStepsCount: number;
  totalStepsCount: number;
  stageProgressPercent: number;
}

/**
 * DTO Tiến độ sản xuất chi tiết của từng dòng sản phẩm (OrderItem) trên đơn hàng (ProductionTrackingDtos)
 */
export interface OrderItemProgressDto {
  orderItemId: number;
  productId: number;
  productCode: string;
  productName: string;
  orderedQty: number;
  completedQty: number;
  inProgressQty: number;
  blockedQty: number;
  remainingQty: number;
  completionPercent: number;
  itemStatus: string;
  totalBatches: number;
  doneBatches: number;
  blockedBatches: number;
  batches: BatchStageProgressDto[];
}

/**
 * DTO Tiến độ sản xuất của đơn hàng bán (Sales Order)
 */
export interface OrderProductionProgressDto {
  orderId: number;
  poNumber: string;
  customerName: string;
  spaceId?: number | null;
  orderStatus: string;
  orderDate: string;
  etdDate: string;
  daysUntilEtd: number;
  etdRiskLevel: "ON_TRACK" | "WARNING" | "OVERDUE" | "COMPLETED" | string;
  totalOrderedUnits: number;
  totalCompletedUnits: number;
  totalInProgressUnits: number;
  overallCompletionPercent: number;
  totalOrderItems: number;
  completedOrderItems: number;
  items: OrderItemProgressDto[];
}
