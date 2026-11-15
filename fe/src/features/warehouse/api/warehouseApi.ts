import { apiClient } from "../../../lib/axios";
import type {
  CommitGoodsReceiptPayload,
  GoodsReceiptResult,
  IssueOrderOption,
  OpenPurchaseOrder,
  OrderAllocation,
} from "../types";

/** API nghiệp vụ kho. Không dùng dữ liệu giả: lỗi được ném ra cho bộ xử lý lỗi toàn cục. */
export const warehouseApi = {
  getOpenPurchaseOrders: async (search?: string): Promise<OpenPurchaseOrder[]> =>
    (await apiClient.get("/warehouse/purchase-orders/open", { params: { search: search || undefined } }))
      .data.data,

  commitGoodsReceipt: async (payload: CommitGoodsReceiptPayload): Promise<GoodsReceiptResult> =>
    (await apiClient.post("/warehouse/goods-receipts", payload)).data.data,

  getOrdersForIssue: async (search?: string): Promise<IssueOrderOption[]> =>
    (
      await apiClient.get("/orders", {
        params: { searchKeyword: search || undefined, pageNumber: 1, pageSize: 50 },
      })
    ).data.data.items,

  getOrderAllocations: async (orderId: number): Promise<OrderAllocation[]> =>
    (await apiClient.get(`/mrp/orders/${orderId}/allocations`)).data.data,
};
