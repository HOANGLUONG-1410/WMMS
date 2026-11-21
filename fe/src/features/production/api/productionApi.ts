// fe/src/features/production/api/productionApi.ts
import { apiClient } from "../../../lib/axios";
import type {
  AdvanceStepRequestDto,
  ApproveOverdriveDto,
  CompleteFinishedBatchRequestDto,
  ContinueSubBatchIndependentlyDto,
  ContinueSubBatchResultDto,
  FinishedGoodsReceiptResultDto,
  GenerateBatchesRequestDto,
  MergeSubBatchDto,
  MergeSubBatchResultDto,
  OrderProductionProgressDto,
  OverdriveOperationResultDto,
  ProductionBatchDto,
  ProductionOrderSummaryDto,
  QcInspectionResultDto,
  RecordQcInspectionDto,
  ReinspectSubBatchDto,
  ReinspectSubBatchResultDto,
  RequestOverdriveDto,
} from "../types";

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

export const productionApi = {
  /**
   * Lấy danh sách lô sản xuất theo SpaceKey (SC-19)
   * GET /api/v1/production-orders/space/{spaceKey}
   */
  getBatchesBySpace: async (spaceKey: string): Promise<ProductionBatchDto[]> => {
    const res = await apiClient.get<ApiResponse<ProductionBatchDto[]>>(
      `/production-orders/space/${encodeURIComponent(spaceKey)}`
    );
    return res.data.data;
  },

  /**
   * Lấy danh sách lô sản xuất theo OrderItemId (SC-19)
   * GET /api/v1/production-orders/order-item/{orderItemId}
   */
  getBatchesByOrderItem: async (orderItemId: number): Promise<ProductionBatchDto[]> => {
    const res = await apiClient.get<ApiResponse<ProductionBatchDto[]>>(
      `/production-orders/order-item/${orderItemId}`
    );
    return res.data.data;
  },

  /**
   * Lấy chi tiết một lô sản xuất kèm các bước routing (SC-19)
   * GET /api/v1/production-orders/batches/{batchId}
   */
  getBatchById: async (batchId: number): Promise<ProductionBatchDto> => {
    const res = await apiClient.get<ApiResponse<ProductionBatchDto>>(
      `/production-orders/batches/${batchId}`
    );
    return res.data.data;
  },

  /**
   * Phát hành lệnh sản xuất & chia lô thông minh (SC-19, UC-62)
   * POST /api/v1/production-orders/generate-batches
   */
  generateBatches: async (payload: GenerateBatchesRequestDto): Promise<ProductionOrderSummaryDto> => {
    const res = await apiClient.post<ApiResponse<ProductionOrderSummaryDto>>(
      "/production-orders/generate-batches",
      payload
    );
    return res.data.data;
  },

  /**
   * Chuyển công đoạn tiếp theo tuần tự S -> S + 1 (SC-19, QT-05)
   * POST /api/v1/production-orders/batches/{batchId}/advance-step
   */
  advanceStep: async (
    batchId: number,
    payload: AdvanceStepRequestDto
  ): Promise<ProductionBatchDto> => {
    const res = await apiClient.post<ApiResponse<ProductionBatchDto>>(
      `/production-orders/batches/${batchId}/advance-step`,
      payload
    );
    return res.data.data;
  },

  /**
   * Nghiệm thu KCS tại QC Gate (100% Rule & tách nhánh 97%-3%) (SC-19, QT-06, KBN01)
   * POST /api/v1/production-orders/batches/{batchId}/qc-inspect
   */
  qcInspect: async (
    batchId: number,
    payload: RecordQcInspectionDto
  ): Promise<QcInspectionResultDto> => {
    const res = await apiClient.post<ApiResponse<QcInspectionResultDto>>(
      `/production-orders/batches/${batchId}/qc-inspect`,
      payload
    );
    return res.data.data;
  },

  /**
   * Tổ trưởng gửi yêu cầu chạy trước Overdrive / mở khóa sản xuất (SC-20, UC-63)
   * POST /api/v1/production-orders/batches/{batchId}/overdrive/request
   */
  requestOverdrive: async (
    batchId: number,
    payload: RequestOverdriveDto
  ): Promise<OverdriveOperationResultDto> => {
    const res = await apiClient.post<ApiResponse<OverdriveOperationResultDto>>(
      `/production-orders/batches/${batchId}/overdrive/request`,
      payload
    );
    return res.data.data;
  },

  /**
   * Chủ xưởng / Admin phê duyệt hoặc từ chối Overdrive để mở khóa lô LOCKED (SC-20, KBN02)
   * POST /api/v1/production-orders/batches/{batchId}/overdrive/approve
   */
  approveOverdrive: async (
    batchId: number,
    payload: ApproveOverdriveDto
  ): Promise<OverdriveOperationResultDto> => {
    const res = await apiClient.post<ApiResponse<OverdriveOperationResultDto>>(
      `/production-orders/batches/${batchId}/overdrive/approve`,
      payload
    );
    return res.data.data;
  },

  /**
   * Mở khóa chốt chặn Hold Point khi Thủ kho xác nhận xuất kho bổ sung (SC-20)
   * POST /api/v1/production-orders/batches/{batchId}/hold-point/unlock
   */
  unlockHoldPoint: async (batchId: number): Promise<OverdriveOperationResultDto> => {
    const res = await apiClient.post<ApiResponse<OverdriveOperationResultDto>>(
      `/production-orders/batches/${batchId}/hold-point/unlock`
    );
    return res.data.data;
  },

  /**
   * Nghiệm thu lại (Re-inspection) cho thẻ nhánh phụ 3% Rework / Scrap Replenish (SC-20b, KBN01)
   * POST /api/v1/production-orders/batches/{subBatchId}/reinspect
   */
  reinspectSubBatch: async (
    subBatchId: number,
    payload: ReinspectSubBatchDto
  ): Promise<ReinspectSubBatchResultDto> => {
    const res = await apiClient.post<ApiResponse<ReinspectSubBatchResultDto>>(
      `/production-orders/batches/${subBatchId}/reinspect`,
      payload
    );
    return res.data.data;
  },

  /**
   * Tái hòa nhập / Gộp lô phụ đã đạt chuẩn vào lô chính khi cùng công đoạn (SC-20b)
   * POST /api/v1/production-orders/batches/{subBatchId}/merge
   */
  mergeSubBatch: async (
    subBatchId: number,
    payload: MergeSubBatchDto
  ): Promise<MergeSubBatchResultDto> => {
    const res = await apiClient.post<ApiResponse<MergeSubBatchResultDto>>(
      `/production-orders/batches/${subBatchId}/merge`,
      payload
    );
    return res.data.data;
  },

  /**
   * Cho phép thẻ nhánh phụ chuyển tiếp độc lập sang công đoạn kế tiếp (SC-20b)
   * POST /api/v1/production-orders/batches/{subBatchId}/continue-independent
   */
  continueSubBatchIndependently: async (
    subBatchId: number,
    payload: ContinueSubBatchIndependentlyDto
  ): Promise<ContinueSubBatchResultDto> => {
    const res = await apiClient.post<ApiResponse<ContinueSubBatchResultDto>>(
      `/production-orders/batches/${subBatchId}/continue-independent`,
      payload
    );
    return res.data.data;
  },

  /**
   * Hoàn tất công đoạn cuối và nhập kho thành phẩm (UC-63, UC-64)
   * POST /api/v1/finished-goods/batches/{batchId}/complete
   */
  completeFinishedBatch: async (
    batchId: number,
    payload: CompleteFinishedBatchRequestDto
  ): Promise<FinishedGoodsReceiptResultDto> => {
    const res = await apiClient.post<ApiResponse<FinishedGoodsReceiptResultDto>>(
      `/finished-goods/batches/${batchId}/complete`,
      payload
    );
    return res.data.data;
  },

  /**
   * Lấy tiến độ sản xuất đơn hàng (UC-64)
   * GET /api/v1/finished-goods/orders/{orderId}/progress
   */
  getOrderProgressById: async (orderId: number): Promise<OrderProductionProgressDto> => {
    const res = await apiClient.get<ApiResponse<OrderProductionProgressDto>>(
      `/finished-goods/orders/${orderId}/progress`
    );
    return res.data.data;
  },
};
