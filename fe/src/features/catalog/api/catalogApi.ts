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
   * kết hợp tồn kho thực tế từ GET /api/v1/inventory/stocks (hoặc fallback mặc định).
   * Phân loại theo 4 nhóm: LUMBER, HARDWARE, FINISH_PAINT, PACKAGING
   */
  getMaterials: async (group?: string): Promise<MaterialCatalogDto[]> => {
    type RawMaterialItem = Partial<MaterialCatalogDto> & {
      physicalQty?: number;
      availableQty?: number;
    };

    const normalizeMaterialItem = (item: RawMaterialItem): MaterialCatalogDto => {
      const matchedFallback = DEFAULT_MASTER_MATERIALS.find(
        (d) =>
          d.materialCode.toUpperCase() === (item.materialCode || "").toUpperCase() ||
          d.materialId === item.materialId,
      );

      return {
        materialId: item.materialId ?? matchedFallback?.materialId ?? 0,
        materialCode: item.materialCode ?? matchedFallback?.materialCode ?? "",
        materialName: item.materialName ?? matchedFallback?.materialName ?? "",
        materialGroup: item.materialGroup ?? matchedFallback?.materialGroup ?? "LUMBER",
        unitOfMeasure: item.unitOfMeasure ?? matchedFallback?.unitOfMeasure ?? "PCS",
        dimensionSpec: item.dimensionSpec ?? matchedFallback?.dimensionSpec ?? null,
        fscCertified: item.fscCertified ?? matchedFallback?.fscCertified ?? false,
        safetyStockQty: Number(item.safetyStockQty ?? matchedFallback?.safetyStockQty ?? 0),
        currentStockQty: Number(
          item.currentStockQty ??
            item.physicalQty ??
            item.availableQty ??
            matchedFallback?.currentStockQty ??
            0,
        ),
        status: item.status ?? matchedFallback?.status ?? "ACTIVE",
      };
    };

    // 1. Ưu tiên lấy từ /inventory/stocks vì có sẵn cả dimensionSpec + physicalQty thực tế trong DB
    try {
      const stockParams: Record<string, string | number> = { page: 1, pageSize: 100 };
      if (group && group !== "ALL") {
        stockParams.category = group;
      }
      const stockRes = await apiClient.get<{
        success: boolean;
        data: RawMaterialItem[] | { items?: RawMaterialItem[] };
      }>("/inventory/stocks", { params: stockParams });

      const rawStockData = stockRes.data?.data;
      const stockList = Array.isArray(rawStockData) ? rawStockData : rawStockData?.items ?? [];
      if (stockList.length > 0) {
        return stockList.map(normalizeMaterialItem);
      }
    } catch {
      // Nếu role không thuộc quyền /inventory/stocks thì tiếp tục gọi /materials
    }

    // 2. Gọi /materials (Master Data SC-04) và ánh xạ đầy đủ currentStockQty & dimensionSpec
    try {
      const params: Record<string, string | number> = { page: 1, pageSize: 100, status: "ACTIVE" };
      if (group && group !== "ALL") {
        params.category = group;
      }
      const res = await apiClient.get<{
        success: boolean;
        data: RawMaterialItem[] | { items?: RawMaterialItem[] };
      }>("/materials", { params });

      const rawData = res.data?.data;
      const list = Array.isArray(rawData) ? rawData : rawData?.items ?? [];
      if (list.length > 0) {
        return list.map(normalizeMaterialItem);
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
