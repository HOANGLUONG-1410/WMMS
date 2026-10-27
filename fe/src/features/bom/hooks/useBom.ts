import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { bomApi } from "../api/bomApi";
import type {
  BomDto,
  CalculateDemandRequestDto,
  CreateBomRequestDto,
  RoutingStageDto,
  UpdateBomRequestDto,
  VisualCatalogProductDto,
} from "../types";

export const DEFAULT_BENCHMARK_ROUTING: RoutingStageDto[] = [
  { stageId: 1, stageSequence: 1, stageName: "Rough Cutting (Cắt/Xẻ)", isHoldPoint: false },
  { stageId: 2, stageSequence: 2, stageName: "Planing & Sanding (Bào & Chà nhám)", isHoldPoint: false },
  { stageId: 3, stageSequence: 3, stageName: "Tenon & Mortise (Mộng/Khoan)", isHoldPoint: false },
  { stageId: 4, stageSequence: 4, stageName: "Structure Assembly (Lắp ráp)", isHoldPoint: true },
  { stageId: 5, stageSequence: 5, stageName: "PU Spray & Coating (Sơn PU)", isHoldPoint: false },
  { stageId: 6, stageSequence: 6, stageName: "Final Packaging (Đóng gói)", isHoldPoint: false },
];

export const DEFAULT_BENCHMARK_BOM: BomDto = {
  bomId: 1,
  orderItemId: 1,
  productId: 1,
  productSku: "CHAIR-OAK-01",
  productName: "Nordic Oak Dining Chair",
  categoryName: "CHAIR",
  dimensionsCm: "450 x 520 x 850 mm",
  orderedQty: 400,
  scrapRatePercent: 3.5,
  bomStatus: "DRAFT",
  bomStatusText: "DRAFT",
  updatedAt: "Today 09:40",
  routingStages: DEFAULT_BENCHMARK_ROUTING,
  bomDetails: [
    {
      bomDetailId: 1,
      materialId: 1,
      materialCode: "LUMB-OAK-01",
      materialGroup: "SOLID LUMBER",
      materialName: "American White Oak Rough Board (25 x 120 x 1000 mm)",
      unitOfMeasure: "m³",
      netQtyPerUnit: 0.018,
      grossTotalDemand: 7.45,
      fscCertified: true,
    },
    {
      bomDetailId: 2,
      materialId: 2,
      materialCode: "PLY-BIRCH-02",
      materialGroup: "SHEET WOOD",
      materialName: "Birch Plywood Seat Core (12 x 450 x 480 mm)",
      unitOfMeasure: "Sheet",
      netQtyPerUnit: 0.125,
      grossTotalDemand: 52,
      fscCertified: true,
    },
    {
      bomDetailId: 3,
      materialId: 3,
      materialCode: "HARD-SCREW-04",
      materialGroup: "HARDWARE",
      materialName: "M6 x 50mm Carbon Steel Assembly Screws",
      unitOfMeasure: "Pcs",
      netQtyPerUnit: 8,
      grossTotalDemand: 3312,
      fscCertified: false,
    },
    {
      bomDetailId: 4,
      materialId: 4,
      materialCode: "CHEM-PU-01",
      materialGroup: "FINISHING",
      materialName: "PU Primer & Clear Matt Topcoat Solution",
      unitOfMeasure: "Kg",
      netQtyPerUnit: 0.35,
      grossTotalDemand: 145,
      fscCertified: false,
    },
    {
      bomDetailId: 5,
      materialId: 5,
      materialCode: "PACK-CARTON-08",
      materialGroup: "PACKAGING",
      materialName: "5-Ply Master Export Carton (2 Chairs / Box)",
      unitOfMeasure: "Box",
      netQtyPerUnit: 0.5,
      grossTotalDemand: 207,
      fscCertified: true,
    },
  ],
};

export function useOrderItemBom(orderItemId = 1) {
  return useQuery<BomDto>({
    queryKey: ["bom", "order-item", orderItemId],
    queryFn: async () => {
      try {
        const res = await bomApi.getBomByOrderItemId(orderItemId);
        if (res?.data && res.data.bomDetails?.length > 0) {
          return res.data;
        }
      } catch {
        // Fallback sang benchmark BOM chuẩn Report 3 khi OrderItem chưa có BOM trong DB
      }
      return DEFAULT_BENCHMARK_BOM;
    },
    staleTime: 1000 * 60,
  });
}

export function useVisualCatalogForBom(category?: string) {
  return useQuery<VisualCatalogProductDto[]>({
    queryKey: ["bom", "visual-catalog", category],
    queryFn: async () => {
      try {
        const res = await bomApi.getVisualCatalog(category);
        if (res?.data && res.data.length > 0) {
          return res.data;
        }
      } catch {
        // Fallback nếu mạng gián đoạn
      }
      return [];
    },
    staleTime: 1000 * 60 * 5,
  });
}

export function useCalculateDemand() {
  return useMutation({
    mutationFn: (payload: CalculateDemandRequestDto) => bomApi.calculateDemand(payload),
  });
}

export function useSaveBomDraft() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      bomId,
      createPayload,
      updatePayload,
    }: {
      bomId?: number;
      createPayload: CreateBomRequestDto;
      updatePayload: UpdateBomRequestDto;
    }) => {
      if (bomId && bomId > 0) {
        try {
          return await bomApi.updateBom(bomId, updatePayload);
        } catch {
          return await bomApi.createBom(createPayload);
        }
      }
      return await bomApi.createBom(createPayload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["bom"] });
    },
  });
}

