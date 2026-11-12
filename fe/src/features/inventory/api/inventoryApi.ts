import { apiClient } from "../../../lib/axios";
import {
  DEFAULT_INVENTORY_LEDGER,
  DEFAULT_INVENTORY_STOCKS,
  DEFAULT_MATERIAL_ALLOCATIONS_MAP,
} from "../data/defaultInventoryData";
import type {
  InventoryLedgerFilterDto,
  InventoryStockDetailDto,
  InventoryStockDto,
  InventoryStockFilterDto,
  InventoryTransactionDto,
  PagedResult,
} from "../types";

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data: T;
}

/**
 * Gắn danh sách preview Order Space giữ chỗ cho từng dòng tồn kho
 */
function enrichAllocationsPreview(item: InventoryStockDto): InventoryStockDto {
  if (item.activeAllocationsPreview && item.activeAllocationsPreview.length > 0) {
    return item;
  }

  const matchedDefault = DEFAULT_INVENTORY_STOCKS.find(
    (d) =>
      d.materialCode.toUpperCase() === (item.materialCode || "").toUpperCase() ||
      d.materialId === item.materialId,
  );

  if (matchedDefault?.activeAllocationsPreview && item.allocatedQty > 0) {
    return {
      ...item,
      activeAllocationsPreview: matchedDefault.activeAllocationsPreview,
    };
  }

  if (item.allocatedQty > 0) {
    return {
      ...item,
      activeAllocationsPreview: [
        {
          spaceKey: "SPACE-880504",
          reservedQty: item.allocatedQty,
        },
      ],
    };
  }

  return {
    ...item,
    activeAllocationsPreview: [],
  };
}

/**
 * Lọc danh sách tồn kho cục bộ khi server chưa bật endpoint /inventory/stocks
 */
function filterFallbackStocks(
  filter: InventoryStockFilterDto,
): PagedResult<InventoryStockDto> {
  let list = [...DEFAULT_INVENTORY_STOCKS];

  const groupFilter = (filter.category || filter.group || "").trim().toUpperCase();
  if (groupFilter && groupFilter !== "ALL") {
    list = list.filter((item) => item.materialGroup.toUpperCase() === groupFilter);
  }

  if (filter.search && filter.search.trim()) {
    const kw = filter.search.trim().toLowerCase();
    list = list.filter(
      (item) =>
        item.materialCode.toLowerCase().includes(kw) ||
        item.materialName.toLowerCase().includes(kw) ||
        (item.dimensionSpec || "").toLowerCase().includes(kw),
    );
  }

  if (filter.belowSafetyStock) {
    list = list.filter(
      (item) => item.isBelowSafetyStock || item.availableQty < item.safetyStockQty,
    );
  }

  const page = Math.max(1, filter.page || 1);
  const pageSize = Math.max(1, filter.pageSize || 10);
  const totalCount = list.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const items = list.slice((page - 1) * pageSize, page * pageSize);

  return {
    items,
    totalCount,
    pageNumber: page,
    pageIndex: page,
    pageSize,
    totalPages,
    hasPreviousPage: page > 1,
    hasNextPage: page < totalPages,
  };
}

/**
 * Lọc sổ kho cục bộ khi server chưa bật endpoint /inventory/ledger
 */
function filterFallbackLedger(
  filter: InventoryLedgerFilterDto,
): PagedResult<InventoryTransactionDto> {
  let list = [...DEFAULT_INVENTORY_LEDGER];

  if (filter.materialId && filter.materialId > 0) {
    list = list.filter((tx) => tx.materialId === filter.materialId);
  }

  if (filter.txType && filter.txType.toUpperCase() !== "ALL") {
    const targetType = filter.txType.trim().toUpperCase();
    list = list.filter((tx) => tx.txType.toUpperCase() === targetType);
  }

  if (filter.search && filter.search.trim()) {
    const kw = filter.search.trim().toLowerCase();
    list = list.filter(
      (tx) =>
        (tx.referenceCode || "").toLowerCase().includes(kw) ||
        tx.materialCode.toLowerCase().includes(kw) ||
        tx.materialName.toLowerCase().includes(kw),
    );
  }

  if (filter.startDate) {
    const startTs = new Date(filter.startDate).setHours(0, 0, 0, 0);
    if (!Number.isNaN(startTs)) {
      list = list.filter((tx) => new Date(tx.createdAt).getTime() >= startTs);
    }
  }

  if (filter.endDate) {
    const endTs = new Date(filter.endDate).setHours(23, 59, 59, 999);
    if (!Number.isNaN(endTs)) {
      list = list.filter((tx) => new Date(tx.createdAt).getTime() <= endTs);
    }
  }

  const page = Math.max(1, filter.page || 1);
  const pageSize = Math.max(1, filter.pageSize || 10);
  const totalCount = list.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const items = list.slice((page - 1) * pageSize, page * pageSize);

  return {
    items,
    totalCount,
    pageNumber: page,
    pageIndex: page,
    pageSize,
    totalPages,
    hasPreviousPage: page > 1,
    hasNextPage: page < totalPages,
  };
}

export const inventoryApi = {
  /**
   * SC-12: Truy vấn danh sách tồn kho 3 cấp (Physical - Allocated = Available)
   * Endpoint: GET /api/v1/inventory/stocks
   */
  getStocks: async (
    filter: InventoryStockFilterDto = {},
  ): Promise<PagedResult<InventoryStockDto> & { isLiveServer: boolean }> => {
    try {
      const params: Record<string, string | number | boolean> = {
        page: filter.page || 1,
        pageSize: filter.pageSize || 10,
      };
      if (filter.search?.trim()) params.search = filter.search.trim();
      if (filter.category && filter.category !== "ALL") params.category = filter.category;
      if (filter.group && filter.group !== "ALL") params.group = filter.group;
      if (filter.belowSafetyStock !== undefined) {
        params.belowSafetyStock = filter.belowSafetyStock;
      }

      const res = await apiClient.get<ApiResponse<PagedResult<InventoryStockDto>>>(
        "/inventory/stocks",
        { params },
      );

      const payload = res.data?.data;
      if (payload && Array.isArray(payload.items)) {
        return {
          ...payload,
          items: payload.items.map((st) => ({
            ...enrichAllocationsPreview(st),
            isBelowSafetyStock:
              st.isBelowSafetyStock ?? st.availableQty < st.safetyStockQty,
          })),
          isLiveServer: true,
        };
      }
    } catch {
      // Fallback sang dữ liệu chuẩn 14 vật tư trong WMMS_DB_Test_Iteration1.sql
    }

    return {
      ...filterFallbackStocks(filter),
      isLiveServer: false,
    };
  },

  /**
   * SC-12 Detail: Truy vấn chi tiết tồn kho 3 cấp & các đơn hàng đang giữ chỗ của 1 vật tư
   * Endpoint: GET /api/v1/inventory/stocks/{materialId}
   */
  getStockByMaterialId: async (materialId: number): Promise<InventoryStockDetailDto> => {
    try {
      const res = await apiClient.get<ApiResponse<InventoryStockDetailDto>>(
        `/inventory/stocks/${materialId}`,
      );
      if (res.data?.data) {
        return res.data.data;
      }
    } catch {
      // Fallback sang chi tiết chuẩn của vật tư
    }

    const baseStock =
      DEFAULT_INVENTORY_STOCKS.find((s) => s.materialId === materialId) ||
      DEFAULT_INVENTORY_STOCKS[0];

    const fallbackAllocations =
      DEFAULT_MATERIAL_ALLOCATIONS_MAP[baseStock.materialId] ||
      (baseStock.activeAllocationsPreview || []).map((p, idx) => ({
        allocationId: idx + 100,
        orderId: idx + 1,
        poNumber: p.poNumber || `ASH-2026-OCT-${p.spaceKey.replace("SPACE-", "")}`,
        spaceKey: p.spaceKey,
        customerName: "Ashley Furniture Industries (Mỹ)",
        orderItemId: idx + 1,
        productSku: "CHAIR-DINING-OAK-01",
        productName: "Sản phẩm xuất khẩu B2B đã chốt BOM",
        requiredQty: p.reservedQty,
        reservedQty: p.reservedQty,
        issuedQty: 0,
        shortageQty: 0,
        allocationStatus: "RESERVED",
        etdDate: "2026-11-20T00:00:00Z",
        allocatedAt: baseStock.updatedAt,
      }));

    const fallbackTxs = DEFAULT_INVENTORY_LEDGER.filter(
      (tx) => tx.materialId === baseStock.materialId,
    );

    return {
      ...baseStock,
      allocations: fallbackAllocations,
      recentTransactions: fallbackTxs,
    };
  },

  /**
   * SC-13: Truy vấn Sổ kho điện tử (Electronic Stock Ledger)
   * Endpoint: GET /api/v1/inventory/ledger
   */
  getLedger: async (
    filter: InventoryLedgerFilterDto = {},
  ): Promise<PagedResult<InventoryTransactionDto> & { isLiveServer: boolean }> => {
    try {
      const params: Record<string, string | number> = {
        page: filter.page || 1,
        pageSize: filter.pageSize || 10,
      };
      if (filter.materialId && filter.materialId > 0) {
        params.materialId = filter.materialId;
      }
      if (filter.txType && filter.txType !== "ALL") {
        params.txType = filter.txType;
      }
      if (filter.search?.trim()) {
        params.search = filter.search.trim();
      }
      if (filter.startDate) {
        params.startDate = filter.startDate;
      }
      if (filter.endDate) {
        params.endDate = filter.endDate;
      }

      const res = await apiClient.get<ApiResponse<PagedResult<InventoryTransactionDto>>>(
        "/inventory/ledger",
        { params },
      );

      const payload = res.data?.data;
      if (payload && Array.isArray(payload.items)) {
        return {
          ...payload,
          isLiveServer: true,
        };
      }
    } catch {
      // Fallback sang 14 giao dịch sổ kho đầu kỳ chuẩn từ WMMS_DB_Test_Iteration1.sql
    }

    return {
      ...filterFallbackLedger(filter),
      isLiveServer: false,
    };
  },
};

