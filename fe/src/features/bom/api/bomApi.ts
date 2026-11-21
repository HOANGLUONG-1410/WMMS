import { apiClient } from "../../../lib/axios";
import type {
  BomDto,
  BomStagingPreviewDto,
  CalculateDemandRequestDto,
  CalculateDemandResponseDto,
  ConfirmBomStagingRequestDto,
  CreateBomRequestDto,
  InheritedBomDto,
  RoutingStageDto,
  UpdateBomRequestDto,
  VisualCatalogProductDto,
} from "../types";

export const bomApi = {
  /** Lấy thông tin BOM theo bomId (GET /api/v1/bom/{bomId}) */
  getBomById: async (bomId: number) => {
    const res = await apiClient.get<{ success: boolean; data: BomDto; message?: string }>(
      `/bom/${bomId}`,
    );
    return res.data;
  },

  /** Lấy thông tin BOM của một dòng sản phẩm đơn hàng (GET /api/v1/bom/order-item/{orderItemId}) */
  getBomByOrderItemId: async (orderItemId: number) => {
    const res = await apiClient.get<{ success: boolean; data: BomDto; message?: string }>(
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
    const res = await apiClient.get<{
      success: boolean;
      data: VisualCatalogProductDto[];
      message?: string;
    }>("/bom/visual-catalog", { params });
    return res.data;
  },

  /** Lấy quy trình sản xuất mẫu Default Routing của sản phẩm (GET /api/v1/bom/default-routing/{productId}) */
  getDefaultRouting: async (productId: number) => {
    const res = await apiClient.get<{ success: boolean; data: RoutingStageDto[]; message?: string }>(
      `/bom/default-routing/${productId}`,
    );
    return res.data;
  },

  /** Lấy cấu hình quy trình động hiện tại của sản phẩm (GET /api/v1/bom/routing/{productId}) */
  getProductRouting: async (productId: number) => {
    const res = await apiClient.get<{ success: boolean; data: RoutingStageDto[]; message?: string }>(
      `/bom/routing/${productId}`,
    );
    return res.data;
  },

  /** Lưu cấu hình quy trình động cho sản phẩm (PUT /api/v1/bom/routing/{productId}) */
  saveProductRouting: async (productId: number, stages: RoutingStageDto[]) => {
    const res = await apiClient.put<{ success: boolean; data: RoutingStageDto[]; message?: string }>(
      `/bom/routing/${productId}`,
      { stages },
    );
    return res.data;
  },

  /** Xem trước tính toán tổng nhu cầu nguyên vật liệu có hao hụt (POST /api/v1/bom/calculate-demand) */
  calculateDemand: async (payload: CalculateDemandRequestDto) => {
    const res = await apiClient.post<{
      success: boolean;
      data: CalculateDemandResponseDto;
      message?: string;
    }>("/bom/calculate-demand", payload);
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

  /** Upload file Excel BOM để kiểm tra Staging trước khi lưu (POST /api/v1/bom/staging-preview) */
  previewBomStaging: async (file: File, orderItemId: number, productId: number) => {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("orderItemId", String(orderItemId));
    formData.append("productId", String(productId));

    const res = await apiClient.post<{
      success: boolean;
      data: BomStagingPreviewDto;
      message?: string;
    }>("/bom/staging-preview", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
    return res.data;
  },

  /** Xác nhận dữ liệu BOM Staging và lưu tạo BOM chính thức (POST /api/v1/bom/staging-confirm) */
  confirmBomStaging: async (payload: ConfirmBomStagingRequestDto) => {
    const res = await apiClient.post<{ success: boolean; data: BomDto; message?: string }>(
      "/bom/staging-confirm",
      payload,
    );
    return res.data;
  },
};
