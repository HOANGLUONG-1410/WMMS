import { apiClient } from "../../../lib/axios";
import type {
  BomDto,
  CalculateDemandRequestDto,
  CalculateDemandResponseDto,
  CreateBomRequestDto,
  InheritedBomDto,
  RoutingStageDto,
  UpdateBomRequestDto,
  VisualCatalogProductDto,
} from "../types";

export const bomApi = {
  /** Lấy thông tin BOM theo bomId (GET /api/v1/bom/{bomId}) */
  getBomById: async (bomId: number) => {
    const res = await apiClient.get<{ success: boolean; data: BomDto }>(`/bom/${bomId}`);
    return res.data;
  },

  /** Lấy thông tin BOM của một dòng sản phẩm đơn hàng (GET /api/v1/bom/order-item/{orderItemId}) */
  getBomByOrderItemId: async (orderItemId: number) => {
    const res = await apiClient.get<{ success: boolean; data: BomDto }>(
      `/bom/order-item/${orderItemId}`,
    );
    return res.data;
  },

  /** Kiểm tra sản phẩm đã có BOM sẵn trong hệ thống chưa để kế thừa (GET /api/v1/bom/check-product/{productId}) */
  checkProductBom: async (productId: number) => {
    const res = await apiClient.get<{ success: boolean; data: InheritedBomDto; message?: string }>(
      `/bom/check-product/${productId}`,
    );
    return res.data;
  },

  /** Lấy danh mục sản phẩm trực quan Visual Catalog (GET /api/v1/bom/visual-catalog) */
  getVisualCatalog: async (category?: string) => {
    const params = category && category !== "ALL" ? { category } : {};
    const res = await apiClient.get<{ success: boolean; data: VisualCatalogProductDto[] }>(
      "/bom/visual-catalog",
      { params },
    );
    return res.data;
  },

  /** Lấy quy trình sản xuất mẫu Default Routing của sản phẩm (GET /api/v1/bom/default-routing/{productId}) */
  getDefaultRouting: async (productId: number) => {
    const res = await apiClient.get<{ success: boolean; data: RoutingStageDto[] }>(
      `/bom/default-routing/${productId}`,
    );
    return res.data;
  },

  /** Xem trước tính toán tổng nhu cầu nguyên vật liệu có hao hụt (POST /api/v1/bom/calculate-demand) */
  calculateDemand: async (payload: CalculateDemandRequestDto) => {
    const res = await apiClient.post<{ success: boolean; data: CalculateDemandResponseDto }>(
      "/bom/calculate-demand",
      payload,
    );
    return res.data;
  },

  /** Khởi tạo BOM mới ở trạng thái Draft kèm quy trình tùy biến (POST /api/v1/bom) */
  createBom: async (payload: CreateBomRequestDto) => {
    const res = await apiClient.post<{ success: boolean; data: BomDto; message?: string }>(
      "/bom",
      payload,
    );
    return res.data;
  },

  /** Cập nhật thông tin BOM (PUT /api/v1/bom/{bomId}) */
  updateBom: async (bomId: number, payload: UpdateBomRequestDto) => {
    const res = await apiClient.put<{ success: boolean; data: BomDto; message?: string }>(
      `/bom/${bomId}`,
      payload,
    );
    return res.data;
  },

  /** Phê duyệt / Chốt khóa BOM sang READY_FOR_PRODUCTION (PATCH /api/v1/bom/{bomId}/approve) */
  approveBom: async (bomId: number) => {
    const res = await apiClient.patch<{ success: boolean; data: BomDto; message?: string }>(
      `/bom/${bomId}/approve`,
    );
    return res.data;
  },
};

