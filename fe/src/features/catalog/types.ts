/**
 * Cấu trúc DTO cho Danh mục Sản phẩm Trực quan (Visual Product Catalog - QT-02)
 */
export interface VisualCatalogProductDto {
  productId: number;
  productSku: string;
  productName: string;
  categoryName: string;
  dimensionsCm: string;
  woodSpecies?: string | null;
  finishColor?: string | null;
  imageUrl?: string | null;
  cadDrawingUrl?: string | null;
  hasActiveBom: boolean;
  defaultRoutingStages?: RoutingStageDto[];
}

/**
 * Cấu trúc công đoạn sản xuất mẫu (Routing Stage)
 */
export interface RoutingStageDto {
  stageId: number;
  stageSequence: number;
  stageName: string;
  isHoldPoint: boolean;
}

/**
 * Các nhóm phân loại nguyên vật liệu chính của xưởng gỗ
 */
export type MaterialGroupType = "LUMBER" | "HARDWARE" | "FINISH_PAINT" | "PACKAGING";

/**
 * Cấu trúc DTO cho Danh mục Nguyên vật liệu xưởng (Material Catalog)
 */
export interface MaterialCatalogDto {
  materialId: number;
  materialCode: string;
  materialName: string;
  materialGroup: MaterialGroupType | string;
  unitOfMeasure: string;
  dimensionSpec?: string | null;
  fscCertified: boolean;
  safetyStockQty: number;
  currentStockQty: number;
  status: "ACTIVE" | "INACTIVE" | string;
}

/**
 * Tham số lọc cho danh mục sản phẩm
 */
export interface ProductFilterParams {
  category?: string;
  searchTerm?: string;
}

/**
 * Tham số lọc cho danh mục nguyên vật liệu
 */
export interface MaterialFilterParams {
  group?: string;
  searchTerm?: string;
}

