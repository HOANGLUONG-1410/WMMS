import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { purchasingApi } from "../api/purchasingApi";
import type {
  CreateSupplierPoDto,
  PurchaseOrderQueryParams,
  SupplierPurchaseOrderDto,
  UpdatePoEtaDto,
} from "../types";

export function usePurchaseOrders(params: PurchaseOrderQueryParams = {}) {
  return useQuery<SupplierPurchaseOrderDto[]>({
    queryKey: ["purchase-orders", params],
    queryFn: () => purchasingApi.getPurchaseOrders(params),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 30,
  });
}

export function useCreatePurchaseOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateSupplierPoDto) => purchasingApi.createPurchaseOrder(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
    },
  });
}

export function useBatchIssuePurchaseOrders() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => purchasingApi.batchIssuePurchaseOrders(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
    },
  });
}

export function useUpdatePoEta() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: UpdatePoEtaDto) => purchasingApi.updatePoEta(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
    },
  });
}

