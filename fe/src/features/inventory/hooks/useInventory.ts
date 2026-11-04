import { useQuery } from "@tanstack/react-query";
import { inventoryApi } from "../api/inventoryApi";
import type { InventoryLedgerFilterDto, InventoryStockFilterDto } from "../types";

export const INVENTORY_QUERY_KEYS = {
  stocks: (filter: InventoryStockFilterDto) => ["inventory", "stocks", filter] as const,
  stockDetail: (materialId: number) => ["inventory", "stock-detail", materialId] as const,
  ledger: (filter: InventoryLedgerFilterDto) => ["inventory", "ledger", filter] as const,
};

export function useInventoryStocks(filter: InventoryStockFilterDto) {
  return useQuery({
    queryKey: INVENTORY_QUERY_KEYS.stocks(filter),
    queryFn: () => inventoryApi.getStocks(filter),
    staleTime: 15_000,
  });
}

export function useInventoryStockDetail(materialId: number | null) {
  return useQuery({
    queryKey: INVENTORY_QUERY_KEYS.stockDetail(materialId || 0),
    queryFn: () => inventoryApi.getStockByMaterialId(materialId || 0),
    enabled: Boolean(materialId && materialId > 0),
    staleTime: 15_000,
  });
}

export function useInventoryLedger(filter: InventoryLedgerFilterDto) {
  return useQuery({
    queryKey: INVENTORY_QUERY_KEYS.ledger(filter),
    queryFn: () => inventoryApi.getLedger(filter),
    staleTime: 15_000,
  });
}

