import { apiClient } from "../../../lib/axios";
import type { VisualCatalogProductDto, RoutingStageDto, MaterialCatalogDto } from "../types";
import { DEFAULT_MASTER_MATERIALS } from "../data/defaultMaterials";

export const catalogApi = {
  /**
   * QT-02: Lấy danh mục sản phẩm trực quan (Visual Catalog) từ Backend API
   * Endpoint: GET /api/v1/bom/visual-catalog?category=...
   */
  getVisualProducts: async (category?: string): Promise<VisualCatalogProductDto[]> => {
    const params = category && category !== "ALL" ? { category } : {};
    const res = await apiClient.get<{ success: boolean; data: VisualCatalogProductDto[] }>(
      "/bom/visual-catalog",
      { params },
    );
    return res.data?.data ?? [];
  },

  /**
   * QT-02: Lấy quy trình công đoạn sản xuất mẫu (Default Routing) của sản phẩm
   * Endpoint: GET /api/v1/bom/default-routing/{productId}
   */
  getDefaultRouting: async (productId: number): Promise<RoutingStageDto[]> => {
    const res = await apiClient.get<{ success: boolean; data: RoutingStageDto[] }>(
      `/bom/default-routing/${productId}`,
    );
    return res.data?.data ?? [];
  },

  /**
   * Lấy danh mục nguyên vật liệu chuẩn xưởng (Master Raw Materials) từ GET /api/v1/materials
   * Phân loại theo 4 nhóm: LUMBER, HARDWARE, FINISH_PAINT, PACKAGING
   */
  getMaterials: async (group?: string): Promise<MaterialCatalogDto[]> => {
    try {
      const params: Record<string, string | number> = { page: 1, pageSize: 100, status: "ACTIVE" };
      if (group && group !== "ALL") {
        params.category = group;
      }
      const res = await apiClient.get<{
        success: boolean;
        data: MaterialCatalogDto[] | { items?: MaterialCatalogDto[] };
      }>("/materials", { params });

      const rawData = res.data?.data;
      const list = Array.isArray(rawData) ? rawData : rawData?.items ?? [];
      if (list.length > 0) {
        return list;
      }
    } catch {
      // Fallback sang danh mục vật tư chuẩn đồng bộ DB nếu mất kết nối
    }

    if (!group || group === "ALL") {
      return DEFAULT_MASTER_MATERIALS;
    }
    return DEFAULT_MASTER_MATERIALS.filter((m) => m.materialGroup === group);
  },
};
