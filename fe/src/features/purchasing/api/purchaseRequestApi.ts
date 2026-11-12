import { apiClient } from "../../../lib/axios";
import type { PagedResult } from "../../users/types";
import type {
  PurchaseRequestDetail,
  PurchaseRequestListItem,
  PurchaseRequestQueryParams,
} from "../types";

/**
 * Gọi API phiếu yêu cầu mua hàng (PR). Không có dữ liệu giả: lỗi (403/404/409...) được
 * ném ra để bộ xử lý lỗi toàn cục hiển thị thông báo.
 */
export const purchaseRequestApi = {
  getList: async (
    params: PurchaseRequestQueryParams,
  ): Promise<PagedResult<PurchaseRequestListItem>> =>
    (await apiClient.get("/purchase-requests", { params })).data.data,

  getDetail: async (id: number): Promise<PurchaseRequestDetail> =>
    (await apiClient.get(`/purchase-requests/${id}`)).data.data,

  approve: async (id: number, notes?: string): Promise<PurchaseRequestDetail> =>
    (await apiClient.patch(`/purchase-requests/${id}/approve`, { notes: notes || null })).data
      .data,

  reject: async (id: number, reason: string): Promise<PurchaseRequestDetail> =>
    (await apiClient.patch(`/purchase-requests/${id}/reject`, { reason })).data.data,
};
