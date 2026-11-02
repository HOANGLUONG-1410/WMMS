export interface RoutingStageDto {
  stageId: number;
  stageSequence: number;
  stageName: string;
  isHoldPoint: boolean;
  workstationCode?: string;
  qualityGateName?: string;
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
  physicalStock?: number;
  allocatedStock?: number;
  availableStock?: number;
  netDemand?: number;
  isShortage?: boolean;
  subAssemblyName?: string | null;
  componentDimensionSpec?: string | null;
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
  hasMaterialShortage?: boolean;
  shortageMaterialsCount?: number;
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
  materialGroup?: string;
  unitOfMeasure: string;
  netQtyPerUnit: number;
  grossTotalDemand: number;
  physicalStock?: number;
  allocatedStock?: number;
  availableStock?: number;
  netDemand?: number;
  isShortage?: boolean;
  stockCoverageStatus?: string;
}

export interface CalculateDemandResponseDto {
  orderedQty: number;
  scrapRatePercent: number;
  hasAnyShortage?: boolean;
  shortageItemsCount?: number;
  recommendedBomState?: string;
  calculatedItems: CalculateDemandItemResultDto[];
}

export interface BomDetailItemInputDto {
  materialId: number;
  netQtyPerUnit: number;
  subAssemblyName?: string | null;
  componentDimensionSpec?: string | null;
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

export interface BomStagingRowDto {
  rowIndex: number;
  rowNumber?: number;
  materialCode: string;
  materialName: string;
  materialGroup: string;
  unitOfMeasure: string;
  netQtyPerUnit: number;
  subAssemblyName?: string | null;
  componentPartName?: string | null;
  componentDimensionSpec?: string | null;
  rawPiecesPerUnit?: number | null;
  availableStock?: number;
  notes?: string | null;
  resolvedMaterialId?: number | null;
  isValid: boolean;
  errors: string[];
}

export interface BomStagingPreviewDto {
  fileName: string;
  sheetName: string;
  detectedProductSku?: string | null;
  detectedProductDimensions?: string | null;
  totalRows: number;
  validRows: number;
  errorRows: number;
  hasErrors: boolean;
  rows: BomStagingRowDto[];
}

export interface ConfirmBomStagingRequestDto {
  orderItemId: number;
  productId: number;
  scrapRatePercent: number;
  rows: BomStagingRowDto[];
}

export interface OrderSpaceLineItemForBom {
  orderItemId: number;
  productId: number;
  productSku: string;
  productName: string;
  dimensionsSpec?: string | null;
  woodSpecies?: string | null;
  finishSpec?: string | null;
  orderedQty: number;
  fscCertifiedReq: boolean;
  attachedCadFileName?: string | null;
  bomId?: number | null;
  scrapRatePct?: number | null;
  itemStatus: string;
  bomLifecycleBadge: string;
}
