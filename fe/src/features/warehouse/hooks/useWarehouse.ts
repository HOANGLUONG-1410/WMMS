import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { warehouseApi } from "../api/warehouseApi";
import type {
  CommitGoodsReceiptPayload,
  GoodsReceiptResult,
  IssueOrderOption,
  OpenPurchaseOrder,
  OrderAllocation,
} from "../types";

export function useOpenPurchaseOrders(search: string) {
  return useQuery<OpenPurchaseOrder[]>({
    queryKey: ["warehouse", "open-pos", search],
    queryFn: () => warehouseApi.getOpenPurchaseOrders(search),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 15,
  });
}

/** Xác nhận nhập kho; sau đó tải lại PO đang mở, tồn kho, PO và phân bổ liên quan. */
export function useCommitGoodsReceipt() {
  const qc = useQueryClient();
  return useMutation<GoodsReceiptResult, Error, CommitGoodsReceiptPayload>({
    mutationFn: (payload) => warehouseApi.commitGoodsReceipt(payload),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: ["warehouse"] });
      qc.invalidateQueries({ queryKey: ["inventory"] });
      qc.invalidateQueries({ queryKey: ["purchase-orders"] });
    },
  });
}

export function useOrdersForIssue(search: string) {
  return useQuery<IssueOrderOption[]>({
    queryKey: ["warehouse", "issue-orders", search],
    queryFn: () => warehouseApi.getOrdersForIssue(search),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 30,
  });
}

export function useOrderAllocations(orderId: number | null) {
  return useQuery<OrderAllocation[]>({
    queryKey: ["warehouse", "order-allocations", orderId],
    queryFn: () => warehouseApi.getOrderAllocations(orderId as number),
    enabled: orderId !== null,
  });
}

/** Xác nhận xuất kho cấp phát vật tư bàn giao xuống xưởng; sau đó tải lại dữ liệu phân bổ, tồn kho và tiến độ. */
export function useIssueOrderAllocations() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, reason }: { orderId: number; reason?: string }) =>
      warehouseApi.issueOrderAllocations(orderId, reason),
    onSuccess: (_, variables) => {
      qc.invalidateQueries({ queryKey: ["warehouse", "order-allocations", variables.orderId] });
      qc.invalidateQueries({ queryKey: ["warehouse", "issue-orders"] });
      qc.invalidateQueries({ queryKey: ["inventory"] });
      qc.invalidateQueries({ queryKey: ["production"] });
    },
  });
}
