import { useState } from "react";
import { useNavigate } from "react-router-dom";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import StockBalanceTable from "../components/StockBalanceTable";
import StockDetailModal from "../components/StockDetailModal";
import StockLedgerTable from "../components/StockLedgerTable";
import { DEFAULT_INVENTORY_STOCKS } from "../data/defaultInventoryData";
import { useInventoryLedger, useInventoryStocks } from "../hooks/useInventory";
import type {
  InventoryLedgerFilterDto,
  InventoryStockDto,
  InventoryStockFilterDto,
} from "../types";

export default function InventoryStockPage() {
  const navigate = useNavigate();

  // Tab điều hướng chính: "balance" (Tab 1: Tồn kho 3 cấp) | "ledger" (Tab 2: Sổ kho điện tử)
  const [activeTab, setActiveTab] = useState<"balance" | "ledger">("balance");

  // Kho vật lý đang chọn (theo Mockup 7.1)
  const [selectedYard, setSelectedYard] = useState<string>(
    "Main Raw Material & Lumber Yard (Ha Nam)",
  );

  // Bộ lọc Tab 1: GET /api/v1/inventory/stocks (mặc định 10 dòng/trang giống UserManagementPage)
  const [stockFilter, setStockFilter] = useState<InventoryStockFilterDto>({
    page: 1,
    pageSize: 10,
  });

  // Bộ lọc Tab 2: GET /api/v1/inventory/ledger (mặc định 10 dòng/trang giống UserManagementPage)
  const [ledgerFilter, setLedgerFilter] = useState<InventoryLedgerFilterDto>({
    page: 1,
    pageSize: 10,
  });

  // Modal chi tiết giữ chỗ & sổ kho của 1 vật tư (khi bấm nút Ledger trên dòng)
  const [inspectingStock, setInspectingStock] = useState<InventoryStockDto | null>(null);

  // Gọi API GET /api/v1/inventory/stocks & GET /api/v1/inventory/ledger
  const {
    data: stocksResult,
    isLoading: isLoadingStocks,
    isFetching: isFetchingStocks,
    refetch: refetchStocks,
  } = useInventoryStocks(stockFilter);

  // Lấy danh mục tổng từ Backend (pageSize=100) để hiển thị chính xác 3 thẻ KPI tổng & dropdown lọc vật tư dù đang ở trang 1/2
  const { data: allStocksResult, refetch: refetchAllStocks } = useInventoryStocks({
    page: 1,
    pageSize: 100,
  });

  const {
    data: ledgerResult,
    isLoading: isLoadingLedger,
    isFetching: isFetchingLedger,
    refetch: refetchLedger,
  } = useInventoryLedger(ledgerFilter);

  const stocks = stocksResult?.items || [];
  const totalStocksCount = stocksResult?.totalCount ?? stocks.length;
  const totalStockPages = stocksResult?.totalPages;

  const ledgerTransactions = ledgerResult?.items || [];
  const totalLedgerCount = ledgerResult?.totalCount ?? ledgerTransactions.length;
  const totalLedgerPages = ledgerResult?.totalPages;

  // Tính toán các chỉ số KPI trên thẻ Header dựa trên toàn bộ 14 vật tư từ Backend
  const referenceAllStocks =
    allStocksResult?.items && allStocksResult.items.length > 0
      ? allStocksResult.items
      : DEFAULT_INVENTORY_STOCKS;

  const totalSkuCount = allStocksResult?.totalCount ?? referenceAllStocks.length;
  const allocatedItemsCount = referenceAllStocks.filter((s) => s.allocatedQty > 0).length;
  const belowSafetyCount = referenceAllStocks.filter(
    (s) => s.isBelowSafetyStock || s.availableQty < s.safetyStockQty || s.availableQty <= 0,
  ).length;

  // Chuyển từ Modal chi tiết vật tư sang Tab 2 (Sổ kho điện tử) lọc sẵn theo vật tư đó
  const handleJumpToFullLedger = (materialId: number) => {
    setLedgerFilter({
      materialId,
      page: 1,
      pageSize: ledgerFilter.pageSize || 10,
    });
    setActiveTab("ledger");
  };

  // Xuất dữ liệu ra CSV phục vụ báo cáo / Demo
  const handleExportCsv = () => {
    if (activeTab === "balance") {
      const headers = [
        "MaterialCode",
        "MaterialName",
        "MaterialGroup",
        "UoM",
        "PhysicalQty",
        "AllocatedQty",
        "AvailableQty",
        "SafetyStockQty",
      ];
      const rowsCsv = stocks.map((s) =>
        [
          s.materialCode,
          `"${s.materialName.replace(/"/g, '""')}"`,
          s.materialGroup,
          s.unitOfMeasure,
          s.physicalQty,
          s.allocatedQty,
          s.availableQty,
          s.safetyStockQty,
        ].join(","),
      );
      const csvContent = "\uFEFF" + [headers.join(","), ...rowsCsv].join("\n");
      const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `WMMS_3Tier_Inventory_Balance_${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } else {
      const headers = [
        "TransactionId",
        "CreatedAt",
        "TxType",
        "ReferenceCode",
        "MaterialCode",
        "MaterialName",
        "Quantity",
        "UoM",
      ];
      const rowsCsv = ledgerTransactions.map((tx) =>
        [
          tx.transactionId,
          tx.createdAt,
          tx.txType,
          tx.referenceCode || "",
          tx.materialCode,
          `"${tx.materialName.replace(/"/g, '""')}"`,
          tx.quantity,
          tx.unitOfMeasure,
        ].join(","),
      );
      const csvContent = "\uFEFF" + [headers.join(","), ...rowsCsv].join("\n");
      const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `WMMS_Stock_Ledger_${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    }
  };

  return (
    <WmmsAppShell>
      <div className="max-w-[1400px] mx-auto space-y-4">
        {/* Top Context Bar: Warehouse Yard Selector (Theo đúng Mockup 7.1) */}
        <div className="bg-white border border-slate-200 rounded-lg px-4 py-2.5 flex flex-wrap items-center justify-between gap-3 shadow-2xs">
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span className="font-semibold text-slate-600">Warehouse Yard:</span>
            <select
              value={selectedYard}
              onChange={(e) => setSelectedYard(e.target.value)}
              className="px-2.5 py-1 text-xs font-bold text-slate-900 bg-slate-50 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="Main Raw Material & Lumber Yard (Ha Nam)">
                Main Raw Material &amp; Lumber Yard (Ha Nam)
              </option>
              <option value="Kiln-Dried Timber & Veneer Warehouse (Zone B)">
                Kiln-Dried Timber &amp; Veneer Warehouse (Zone B)
              </option>
              <option value="Hardware, Paint & Packaging Store (Zone C)">
                Hardware, Paint &amp; Packaging Store (Zone C)
              </option>
            </select>
          </div>

          <div className="flex items-center gap-2 text-xs">
            <span className="px-2 py-0.5 rounded bg-emerald-50 text-emerald-700 border border-emerald-200 font-mono text-[11px] font-bold">
              ● MES 3-TIER ENGINE ACTIVE
            </span>
            <button
              type="button"
              onClick={() => {
                refetchStocks();
                refetchAllStocks();
                refetchLedger();
              }}
              className="px-2.5 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded text-[11px] font-semibold transition"
            >
              ↻ Refresh Data
            </button>
            <button
              type="button"
              onClick={handleExportCsv}
              className="px-2.5 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded text-[11px] font-semibold transition"
            >
              📥 Export CSV
            </button>
          </div>
        </div>

        {/* Page Title & Action Buttons (Fields #1 & #2 in Report 3 - 7.1) */}
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
              Inventory 3-Tier Stock Grid (SCR-INV-01)
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Strict mathematical separation of Physical, Reserved (Allocated), and Free Available
              stock to prevent double-counting.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5 shrink-0">
            {/* Field #1: + Inbound Receipt (Commit) */}
            <button
              type="button"
              onClick={() => navigate("/warehouse/receipts")}
              className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-bold rounded-md shadow-xs transition"
            >
              + Inbound Receipt (Commit)
            </button>

            {/* Field #2: Goods Issue to Floor */}
            <button
              type="button"
              onClick={() => navigate("/warehouse/issues")}
              className="px-4 py-2 bg-white hover:bg-slate-50 text-slate-800 border border-slate-300 text-xs font-semibold rounded-md shadow-2xs transition"
            >
              Goods Issue to Floor
            </button>
          </div>
        </div>

        {/* Formula Callout Banner (Theo đúng Mockup 7.1) */}
        <div className="bg-white border border-slate-200 rounded-xl px-4 py-3 flex flex-wrap items-center justify-between gap-3 shadow-2xs">
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span className="text-sm">💡</span>
            <strong className="font-extrabold text-slate-900">
              Three-Tier Core Inventory Formula (BR-003):
            </strong>
            <code className="font-mono text-[11px] font-bold text-slate-800 bg-slate-100 px-2.5 py-1 rounded border border-slate-200">
              Available Stock = Total Physical Stock - Allocated Stock (Reserved)
            </code>
          </div>

          <span className="px-2.5 py-1 text-[10px] font-extrabold uppercase tracking-wider rounded bg-emerald-100 text-emerald-800 border border-emerald-300">
            DOUBLE-COUNTING BLOCKED
          </span>
        </div>

        {/* 3 KPI Summary Cards (Theo đúng Mockup 7.1) */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {/* Card 1: TOTAL PHYSICAL STOCK ITEMS */}
          <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
              TOTAL PHYSICAL STOCK ITEMS
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1.5">
              {totalSkuCount} SKU
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Physical inventory in warehouse racks
            </div>
          </div>

          {/* Card 2: TOTAL ALLOCATED STOCK */}
          <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
              TOTAL ALLOCATED STOCK
            </div>
            <div className="text-2xl font-extrabold text-[#d97706] mt-1.5">
              {allocatedItemsCount} Items Locked
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Reserved strictly for active orders
            </div>
          </div>

          {/* Card 3: BELOW SAFETY BUFFER */}
          <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
              BELOW SAFETY BUFFER
            </div>
            <div className="text-2xl font-extrabold text-rose-600 mt-1.5">
              {belowSafetyCount} Items
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Included in daily purchase aggregation
            </div>
          </div>
        </div>

        {/* Thanh chuyển đổi 2 Tabs chính: Tab 1 (Tồn kho 3 cấp) & Tab 2 (Sổ kho điện tử) */}
        <div className="border-b border-slate-200 flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setActiveTab("balance")}
              className={`px-4 py-2.5 text-xs font-extrabold border-b-2 transition flex items-center gap-2 ${
                activeTab === "balance"
                  ? "border-[#2563eb] text-[#2563eb] bg-blue-50/40 rounded-t-lg"
                  : "border-transparent text-slate-600 hover:text-slate-900"
              }`}
            >
              <span>🏬</span>
              <span>Tab 1: Bảng cân đối tồn kho 3 cấp (3-Tier Stock Balance)</span>
              <span className="px-1.5 py-0.5 text-[10px] rounded-full bg-slate-200 text-slate-800">
                {totalStocksCount}
              </span>
            </button>

            <button
              type="button"
              onClick={() => setActiveTab("ledger")}
              className={`px-4 py-2.5 text-xs font-extrabold border-b-2 transition flex items-center gap-2 ${
                activeTab === "ledger"
                  ? "border-[#2563eb] text-[#2563eb] bg-blue-50/40 rounded-t-lg"
                  : "border-transparent text-slate-600 hover:text-slate-900"
              }`}
            >
              <span>📒</span>
              <span>Tab 2: Sổ kho điện tử (Electronic Stock Ledger)</span>
              <span className="px-1.5 py-0.5 text-[10px] rounded-full bg-slate-200 text-slate-800">
                {totalLedgerCount}
              </span>
            </button>
          </div>

          <div className="text-[11px] text-slate-400 pb-2">
            API:{" "}
            <code className="font-mono text-slate-600">
              {activeTab === "balance"
                ? "GET /api/v1/inventory/stocks"
                : "GET /api/v1/inventory/ledger"}
            </code>
          </div>
        </div>

        {/* Nội dung Tab đang chọn */}
        {activeTab === "balance" ? (
          <StockBalanceTable
            stocks={stocks}
            totalCount={totalStocksCount}
            totalPages={totalStockPages}
            isLoading={isLoadingStocks}
            isFetching={isFetchingStocks}
            filter={stockFilter}
            onChangeFilter={setStockFilter}
            onInspectStockLedger={(item) => setInspectingStock(item)}
          />
        ) : (
          <StockLedgerTable
            transactions={ledgerTransactions}
            allMaterials={referenceAllStocks}
            totalCount={totalLedgerCount}
            totalPages={totalLedgerPages}
            isLoading={isLoadingLedger}
            isFetching={isFetchingLedger}
            filter={ledgerFilter}
            onChangeFilter={setLedgerFilter}
            onResetFilter={() =>
              setLedgerFilter({
                page: 1,
                pageSize: ledgerFilter.pageSize || 10,
              })
            }
          />
        )}
      </div>

      {/* Modal Xem chi tiết đơn hàng giữ chỗ & lịch sử Sổ kho của 1 vật tư */}
      <StockDetailModal
        isOpen={Boolean(inspectingStock)}
        stockItem={inspectingStock}
        onClose={() => setInspectingStock(null)}
        onOpenFullLedgerForMaterial={(materialId) => handleJumpToFullLedger(materialId)}
      />
    </WmmsAppShell>
  );
}

