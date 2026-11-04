import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { bomApi } from "../api/bomApi";
import type {
  BomDto,
  CalculateDemandRequestDto,
  ConfirmBomStagingRequestDto,
  CreateBomRequestDto,
  RoutingStageDto,
  UpdateBomRequestDto,
  VisualCatalogProductDto,
} from "../types";

export const DEFAULT_BENCHMARK_ROUTING: RoutingStageDto[] = [
  { stageId: 1, stageSequence: 1, stageName: "1. Pha phôi gỗ thô", isHoldPoint: false },
  { stageId: 2, stageSequence: 2, stageName: "2. Cắt/Xẻ định hình chi tiết", isHoldPoint: false },
  { stageId: 3, stageSequence: 3, stageName: "3. Bào 4 mặt & Chà nhám phẳng", isHoldPoint: false },
  { stageId: 4, stageSequence: 4, stageName: "4. Khoan lỗ & Phay mộng âm/dương", isHoldPoint: false },
  {
    stageId: 5,
    stageSequence: 5,
    stageName: "5. Lắp ráp kết cấu & Bắt phụ kiện bản lề/ray trượt",
    isHoldPoint: true,
  },
  { stageId: 6, stageSequence: 6, stageName: "6. Sơn lót & Sơn phủ hoàn thiện PU", isHoldPoint: false },
  { stageId: 7, stageSequence: 7, stageName: "7. Kiểm tra xuất xưởng & Đóng gói carton", isHoldPoint: false },
];

export const DEFAULT_BENCHMARK_BOM: BomDto = {
  bomId: 0,
  orderItemId: 1,
  productId: 1,
  productSku: "CHAIR-DINING-OAK-01",
  productName: "Ghế ăn tựa nan gỗ sồi Bắc Mỹ",
  categoryName: "CHAIR",
  dimensionsCm: "45 x 52 x 88 cm",
  orderedQty: 400,
  scrapRatePercent: 3.5,
  bomStatus: "DRAFT",
  bomStatusText: "DRAFT",
  updatedAt: null,
  routingStages: DEFAULT_BENCHMARK_ROUTING,
  bomDetails: [
    {
      bomDetailId: 1,
      materialId: 1,
      materialCode: "WOOD_OAK_01",
      materialGroup: "LUMBER",
      materialName: "Gỗ sồi Bắc Mỹ xẻ sấy FAS (Oak)",
      unitOfMeasure: "M3",
      dimensionSpec: "26mm x 150mm x 2400mm",
      netQtyPerUnit: 0.018,
      grossTotalDemand: 7.452,
      fscCertified: true,
    },
    {
      bomDetailId: 2,
      materialId: 3,
      materialCode: "HW_SCREW_M4",
      materialGroup: "HARDWARE",
      materialName: "Vít bắt gỗ tự khoan mạ kẽm M4x30",
      unitOfMeasure: "PCS",
      dimensionSpec: "M4 x 30mm",
      netQtyPerUnit: 8,
      grossTotalDemand: 3312,
      fscCertified: false,
    },
    {
      bomDetailId: 3,
      materialId: 6,
      materialCode: "PAINT_SEALER_01",
      materialGroup: "FINISH_PAINT",
      materialName: "Sơn lót PU 2K kháng ẩm gốc nước",
      unitOfMeasure: "LITER",
      dimensionSpec: "Thùng 20L",
      netQtyPerUnit: 0.2,
      grossTotalDemand: 82.8,
      fscCertified: false,
    },
    {
      bomDetailId: 4,
      materialId: 7,
      materialCode: "PAINT_TOP_MATT",
      materialGroup: "FINISH_PAINT",
      materialName: "Sơn phủ bóng mờ PU 50%",
      unitOfMeasure: "LITER",
      dimensionSpec: "Thùng 20L",
      netQtyPerUnit: 0.15,
      grossTotalDemand: 62.1,
      fscCertified: false,
    },
    {
      bomDetailId: 5,
      materialId: 8,
      materialCode: "BOX_CARTON_01",
      materialGroup: "PACKAGING",
      materialName: "Thùng carton 5 lớp xuất khẩu Master",
      unitOfMeasure: "PCS",
      dimensionSpec: "600mm x 550mm x 950mm",
      netQtyPerUnit: 0.5,
      grossTotalDemand: 207,
      fscCertified: true,
    },
  ],
};

/**
 * Truy vấn BOM thực tế của một OrderItem từ Backend (GET /api/v1/bom/order-item/{orderItemId}).
 * Nếu OrderItem chưa có BOM (Backend trả 404), trả về `null` để màn hình chuyển sang trạng thái Tạo mới / Kế thừa.
 */
export function useOrderItemBom(orderItemId: number) {
  return useQuery<BomDto | null>({
    queryKey: ["bom", "order-item", orderItemId],
    enabled: orderItemId > 0,
    queryFn: async () => {
      try {
        const res = await bomApi.getBomByOrderItemId(orderItemId);
        return res?.data ?? null;
      } catch (err) {
        if (isAxiosError(err) && err.response?.status === 404) {
          return null;
        }
        throw err;
      }
    },
    retry: false,
    staleTime: 1000 * 30,
  });
}

export function useVisualCatalogForBom(category?: string) {
  return useQuery<VisualCatalogProductDto[]>({
    queryKey: ["bom", "visual-catalog", category || "ALL"],
    queryFn: async () => {
      const res = await bomApi.getVisualCatalog(category);
      return res?.data ?? [];
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
      bomId?: number | null;
      createPayload: CreateBomRequestDto;
      updatePayload: UpdateBomRequestDto;
    }) => {
      if (bomId && bomId > 0) {
        return await bomApi.updateBom(bomId, updatePayload);
      }
      return await bomApi.createBom(createPayload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["bom"] });
    },
  });
}

export function useApproveBom() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (bomId: number) => bomApi.approveBom(bomId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["bom"] });
    },
  });
}

export function usePreviewBomStaging() {
  return useMutation({
    mutationFn: ({
      file,
      orderItemId,
      productId,
    }: {
      file: File;
      orderItemId: number;
      productId: number;
    }) => bomApi.previewBomStaging(file, orderItemId, productId),
  });
}

export function useConfirmBomStaging() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: ConfirmBomStagingRequestDto) => bomApi.confirmBomStaging(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["bom"] });
    },
  });
}
