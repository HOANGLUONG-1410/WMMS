export interface RoutingStageDto {
  stageId: number;
  stageSequence: number;
  stageName: string;
  isHoldPoint: boolean;
}

export interface BomDetailDto {
  bomDetailId: number;
  bomId?: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  materialGroup: string;
  unitOfMeasure: string;
  dimensionSpec?: string | null;
  fscCertified?: boolean;
  netQtyPerUnit: number;
  grossTotalDemand: number;
  notes?: string | null;
}

export interface BomDto {
  bomId: number;
  orderItemId: number;
  productId: number;
  productSku: string;
  productName: string;
  categoryName: string;
  dimensionsCm: string;
  orderedQty: number;
  scrapRatePercent: number;
  bomStatus: number | string;
  bomStatusText: string;
  createdBy?: number;
  creatorName?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
  bomDetails: BomDetailDto[];
  routingStages: RoutingStageDto[];
}

export interface InheritedBomDto {
  hasExistingBom: boolean;
  sourceBomId?: number | null;
  scrapRatePercent: number;
  details: BomDetailDto[];
  routingStages: RoutingStageDto[];
}

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
  defaultRoutingStages: RoutingStageDto[];
}

export interface CalculateDemandItemDto {
  materialId: number;
  netQtyPerUnit: number;
}

export interface CalculateDemandRequestDto {
  orderedQty: number;
  scrapRatePercent: number;
  items: CalculateDemandItemDto[];
}

export interface CalculateDemandItemResultDto {
  materialId: number;
  materialCode: string;
  materialName: string;
  unitOfMeasure: string;
  netQtyPerUnit: number;
  grossTotalDemand: number;
}

export interface CalculateDemandResponseDto {
  orderedQty: number;
  scrapRatePercent: number;
  calculatedItems: CalculateDemandItemResultDto[];
}

export interface BomDetailItemInputDto {
  materialId: number;
  netQtyPerUnit: number;
  notes?: string | null;
}

export interface CreateBomRequestDto {
  orderItemId: number;
  productId: number;
  scrapRatePercent: number;
  details: BomDetailItemInputDto[];
  customRoutingStages?: RoutingStageDto[];
}

export interface UpdateBomRequestDto {
  scrapRatePercent: number;
  details: BomDetailItemInputDto[];
  customRoutingStages?: RoutingStageDto[];
}

