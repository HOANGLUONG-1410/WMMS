import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { purchaseRequestApi } from "../api/purchaseRequestApi";
import type { PagedResult } from "../../users/types";
import type {
  PurchaseRequestDetail,
  PurchaseRequestListItem,
  PurchaseRequestQueryParams,
} from "../types";

const KEY = "purchase-requests";

export function usePurchaseRequests(params: PurchaseRequestQueryParams) {
  const p = {
    status: params.status || undefined,
    fromDate: params.fromDate || undefined,
    toDate: params.toDate || undefined,
    searchKeyword: params.searchKeyword || undefined,
    pageNumber: params.pageNumber ?? 1,
    pageSize: params.pageSize ?? 10,
  };
  return useQuery<PagedResult<PurchaseRequestListItem>>({
    queryKey: [KEY, "list", p],
    queryFn: () => purchaseRequestApi.getList(p),
    placeholderData: keepPreviousData,
    staleTime: 1000 * 30,
  });
}

export function usePurchaseRequestDetail(id: number | null) {
  return useQuery<PurchaseRequestDetail>({
    queryKey: [KEY, "detail", id],
    queryFn: () => purchaseRequestApi.getDetail(id as number),
    enabled: id !== null,
  });
}

/** Duyệt PR (chỉ Chủ xưởng). Luôn tải lại dữ liệu kể cả khi lỗi 409 (PR đã được xử lý nơi khác). */
export function useApprovePr() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, notes }: { id: number; notes?: string }) =>
      purchaseRequestApi.approve(id, notes),
    onSettled: () => qc.invalidateQueries({ queryKey: [KEY] }),
  });
}

/** Từ chối PR kèm lý do bắt buộc (chỉ Chủ xưởng). */
export function useRejectPr() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) =>
      purchaseRequestApi.reject(id, reason),
    onSettled: () => qc.invalidateQueries({ queryKey: [KEY] }),
  });
}
