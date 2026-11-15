import { apiClient } from "../../../lib/axios";
import type {
  AutoAllocationRequestDto,
  AutoAllocationResultDto,
  MaterialOrderAllocationDto,
  MrpAnalysisResultDto,
  ReallocateByEtdRequestDto,
  ReallocateByEtdResultDto,
  ReleaseOrderAllocationsRequestDto,
} from "../types";

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

export const mrpApi = {
  /**
   * SC-14: Phân tích nhu cầu nguyên vật liệu MRP toàn xưởng
   * GET /api/v1/mrp/factory-analysis
   */
  analyzeFactoryMrp: async (): Promise<MrpAnalysisResultDto> => {
    const res = await apiClient.get<ApiResponse<MrpAnalysisResultDto>>(
      "/mrp/factory-analysis",
    );
    return res.data.data;
  },

  /**
   * SC-14: Phân tích MRP chi tiết cho một Không gian Đơn hàng (Order Space)
   * GET /api/v1/mrp/spaces/{spaceId}/analysis
   */
  analyzeOrderSpaceMrp: async (spaceId: number | string): Promise<MrpAnalysisResultDto> => {
    const res = await apiClient.get<ApiResponse<MrpAnalysisResultDto>>(
      `/mrp/spaces/${spaceId}/analysis`,
    );
    return res.data.data;
  },

  /**
   * SC-14: Phân tích MRP chi tiết cho một Đơn hàng (sales_orders)
   * GET /api/v1/mrp/orders/{orderId}/analysis
   */
  analyzeOrderMrp: async (orderId: number | string): Promise<MrpAnalysisResultDto> => {
    const res = await apiClient.get<ApiResponse<MrpAnalysisResultDto>>(
      `/mrp/orders/${orderId}/analysis`,
    );
    return res.data.data;
  },

  /**
   * SC-14: Thực thi thuật toán Phân bổ tồn kho tự động (Auto-Allocation) cho Order Space
   * POST /api/v1/mrp/spaces/{spaceId}/auto-allocate
   */
  autoAllocateOrderSpace: async (
    spaceId: number | string,
    payload?: AutoAllocationRequestDto,
  ): Promise<AutoAllocationResultDto> => {
    const res = await apiClient.post<ApiResponse<AutoAllocationResultDto>>(
      `/mrp/spaces/${spaceId}/auto-allocate`,
      payload || { autoQueueShortageToDailyPr: true },
    );
    return res.data.data;
  },

  /**
   * SC-14: Thực thi thuật toán Phân bổ tồn kho tự động cho Đơn hàng
   * POST /api/v1/mrp/orders/{orderId}/auto-allocate
   */
  autoAllocateOrder: async (
    orderId: number | string,
    payload?: AutoAllocationRequestDto,
  ): Promise<AutoAllocationResultDto> => {
    const res = await apiClient.post<ApiResponse<AutoAllocationResultDto>>(
      `/mrp/orders/${orderId}/auto-allocate`,
      payload || { autoQueueShortageToDailyPr: true },
    );
    return res.data.data;
  },

  /**
   * Tái phân bổ tồn kho khả dụng theo độ ưu tiên hạn giao hàng ETD sớm nhất (FIFO by ETD)
   * POST /api/v1/mrp/reallocate-by-etd
   */
  reallocateByEtd: async (
    payload?: ReallocateByEtdRequestDto,
  ): Promise<ReallocateByEtdResultDto> => {
    const res = await apiClient.post<ApiResponse<ReallocateByEtdResultDto>>(
      "/mrp/reallocate-by-etd",
      payload || {},
    );
    return res.data.data;
  },

  /**
   * Giải phóng phần vật tư giữ chỗ (RESERVED) của đơn hàng
   * POST /api/v1/mrp/orders/{orderId}/release-allocations
   */
  releaseOrderAllocations: async (
    orderId: number | string,
    reason?: string,
  ): Promise<MaterialOrderAllocationDto[]> => {
    const res = await apiClient.post<ApiResponse<MaterialOrderAllocationDto[]>>(
      `/mrp/orders/${orderId}/release-allocations`,
      { reason: reason || "Giải phóng giữ chỗ vật tư theo yêu cầu điều độ kế hoạch." } as ReleaseOrderAllocationsRequestDto,
    );
    return res.data.data;
  },

  /**
   * Lấy danh sách phân bổ vật tư của Order Space
   * GET /api/v1/mrp/spaces/{spaceId}/allocations
   */
  getOrderSpaceAllocations: async (
    spaceId: number | string,
  ): Promise<MaterialOrderAllocationDto[]> => {
    const res = await apiClient.get<ApiResponse<MaterialOrderAllocationDto[]>>(
      `/mrp/spaces/${spaceId}/allocations`,
    );
    return res.data.data;
  },
};
