import { useQuery } from "@tanstack/react-query";
import { orderIntakeApi } from "../../../services/wmmsApi";
import { productionApi } from "../../production/api/productionApi";
import type { OrderSpaceLineItemForBom } from "../../bom/types";
import type { ProductionBatchDto } from "../../production/types";

export interface OrderSpaceMilestoneDto {
  title: string;
  timestamp: string;
  actorName: string;
  actorRole: string;
  description: string;
}

export interface OrderSpaceDetailData {
  spaceId?: number;
  spaceKey: string;
  spaceName?: string;
  orderId?: number;
  poNumber: string;
  customerName: string;
  orderDate?: string;
  etdDate?: string;
  daysRemaining?: number;
  isUrgentEtd?: boolean;
  orderStatus?: string;
  lifecycleStatusBadge?: string;
  fscCertified100?: boolean;
  totalOrderedUnits?: number;
  totalOrderedSubtitle?: string;
  bomStatusTitle?: string;
  bomStatusSubtitle?: string;
  materialAllocationTitle?: string;
  materialAllocationSubtitle?: string;
  floorProgressPct?: number;
  floorProgressSubtitle?: string;
  lineItems?: OrderSpaceLineItemForBom[];
  milestones?: OrderSpaceMilestoneDto[];
}

export function useOrderSpaceDetail(spaceKey: string) {
  const spaceQuery = useQuery({
    queryKey: ["order-space", spaceKey],
    queryFn: async (): Promise<OrderSpaceDetailData | null> => {
      if (!spaceKey) return null;
      const res = await orderIntakeApi.getOrderSpaceDetail(spaceKey);
      return (res?.data as OrderSpaceDetailData) ?? null;
    },
    enabled: Boolean(spaceKey),
    staleTime: 1000 * 60 * 5,
    gcTime: 1000 * 60 * 10,
  });

  const batchesQuery = useQuery({
    queryKey: ["order-space-batches", spaceKey],
    queryFn: async (): Promise<ProductionBatchDto[]> => {
      if (!spaceKey) return [];
      try {
        const res = await productionApi.getBatchesBySpace(spaceKey);
        return Array.isArray(res) ? res : [];
      } catch {
        return [];
      }
    },
    enabled: Boolean(spaceKey),
    staleTime: 1000 * 60 * 2,
    gcTime: 1000 * 60 * 5,
  });

  return {
    spaceDetail: spaceQuery.data ?? null,
    batches: batchesQuery.data ?? [],
    isLoading: spaceQuery.isLoading || batchesQuery.isLoading,
    isFetching: spaceQuery.isFetching || batchesQuery.isFetching,
    isError: spaceQuery.isError,
    error: spaceQuery.error,
  };
}
