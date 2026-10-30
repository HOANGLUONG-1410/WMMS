import { useQuery } from "@tanstack/react-query";
import { catalogApi } from "../api/catalogApi";
import type { VisualCatalogProductDto, RoutingStageDto, MaterialCatalogDto } from "../types";

/**
 * Hook truy vấn danh mục sản phẩm hoàn thiện từ Backend
 */
export function useVisualProducts(category?: string) {
  return useQuery<VisualCatalogProductDto[]>({
    queryKey: ["catalog", "products", category],
    queryFn: () => catalogApi.getVisualProducts(category),
    staleTime: 1000 * 60 * 5, // 5 phút cache
  });
}

/**
 * Hook truy vấn quy trình sản xuất mẫu (Default Routing Stages) của một sản phẩm
 */
export function useDefaultRouting(productId: number | null) {
  return useQuery<RoutingStageDto[]>({
    queryKey: ["catalog", "routing", productId],
    queryFn: () => (productId ? catalogApi.getDefaultRouting(productId) : Promise.resolve([])),
    enabled: !!productId,
    staleTime: 1000 * 60 * 10,
  });
}

/**
 * Hook truy vấn danh mục nguyên vật liệu chuẩn xưởng
 */
export function useMaterials(group?: string) {
  return useQuery<MaterialCatalogDto[]>({
    queryKey: ["catalog", "materials", group],
    queryFn: () => catalogApi.getMaterials(group),
    staleTime: 1000 * 60 * 5,
  });
}

