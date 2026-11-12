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
    "Bãi gỗ & nguyên liệu chính (Hà Nam)",
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
      <div className="max-w-[1400px] mx-auto space-y-6">
        {/* Top Context Bar: Warehouse Yard Selector */}
        <div className="bg-white border border-slate-200 rounded-xl px-5 py-3 flex flex-wrap items-center justify-between gap-3 shadow-xs">
          <div className="flex flex-wrap items-center gap-2.5 text-sm">
            <span className="font-semibold text-slate-600">Bãi kho:</span>
            <select
              value={selectedYard}
              onChange={(e) => setSelectedYard(e.target.value)}
              className="px-3 py-1.5 text-sm font-semibold text-slate-900 bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
            >
              <option value="Bãi gỗ & nguyên liệu chính (Hà Nam)">
                Bãi gỗ &amp; nguyên liệu chính (Hà Nam)
              </option>
              <option value="Kho gỗ sấy & veneer (Khu B)">
                Kho gỗ sấy &amp; veneer (Khu B)
              </option>
              <option value="Kho phụ kiện, sơn & bao bì (Khu C)">
                Kho phụ kiện, sơn &amp; bao bì (Khu C)
              </option>
            </select>
          </div>

          <div className="flex items-center gap-2.5 text-sm">
            <button
              type="button"
              onClick={() => {
                refetchStocks();
                refetchAllStocks();
                refetchLedger();
              }}
              className="px-3.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 rounded-lg text-sm font-semibold transition shadow-2xs"
            >
              Làm mới dữ liệu
            </button>
            <button
              type="button"
              onClick={handleExportCsv}
              className="px-3.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 rounded-lg text-sm font-semibold transition shadow-2xs inline-flex items-center gap-1.5"
            >
              <svg className="w-4 h-4 text-slate-500" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="7 10 12 15 17 10" />
                <line x1="12" y1="15" x2="12" y2="3" />
              </svg>
              Xuất CSV
            </button>
          </div>
        </div>

        {/* Page Title & Action Buttons */}
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
              Tồn kho vật tư 3 cấp
            </h1>
            <p className="text-sm text-slate-500 mt-1">
              Tách bạch chặt chẽ giữa tồn vật lý, tồn đã giữ chỗ (phân bổ) và tồn khả dụng để tránh tính trùng.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3 shrink-0">
            {/* + Inbound Receipt (Commit) */}
            <button
              type="button"
              onClick={() => navigate("/warehouse/receipts")}
              className="px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition inline-flex items-center gap-2"
            >
              <svg className="w-4 h-4 stroke-[2.5]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <line x1="12" y1="5" x2="12" y2="19" />
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
              <span>Nhập kho (Xác nhận)</span>
            </button>

            {/* Goods Issue to Floor */}
            <button
              type="button"
              onClick={() => navigate("/warehouse/issues")}
              className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg shadow-2xs transition"
            >
              Xuất kho xuống xưởng
            </button>
          </div>
        </div>

        {/* 3 KPI Summary Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
          {/* Card 1: TOTAL PHYSICAL STOCK ITEMS */}
          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
            <div className="flex items-center justify-between">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                TỔNG MÃ TỒN VẬT LÝ
              </div>
              <span className="p-2 rounded-lg bg-blue-50 text-blue-600">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">
              {totalSkuCount} SKU
            </div>
            <div className="text-xs text-slate-500 mt-1">
              Hàng thực tế trên kệ kho
            </div>
          </div>

          {/* Card 2: TOTAL ALLOCATED STOCK */}
          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
            <div className="flex items-center justify-between">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                TỔNG TỒN ĐÃ PHÂN BỔ
              </div>
              <span className="p-2 rounded-lg bg-amber-50 text-amber-600">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-amber-600 mt-2">
              {allocatedItemsCount} mã đang giữ chỗ
            </div>
            <div className="text-xs text-slate-500 mt-1">
              Chỉ giữ chỗ cho các đơn đang thực hiện
            </div>
          </div>

          {/* Card 3: BELOW SAFETY BUFFER */}
          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
            <div className="flex items-center justify-between">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                DƯỚI MỨC TỒN AN TOÀN
              </div>
              <span className="p-2 rounded-lg bg-rose-50 text-rose-600">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                  <line x1="12" y1="9" x2="12" y2="13" />
                  <line x1="12" y1="17" x2="12.01" y2="17" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-rose-600 mt-2">
              {belowSafetyCount} mã
            </div>
            <div className="text-xs text-slate-500 mt-1">
              Được đưa vào gom mua hàng theo ngày
            </div>
          </div>
        </div>

        {/* Thanh chuyển đổi 2 Tabs chính */}
        <div className="flex border-b border-slate-200 gap-2 bg-white px-5 pt-2 rounded-xl border shadow-xs">
          <button
            type="button"
            onClick={() => setActiveTab("balance")}
            className={`px-4 py-3 text-sm font-bold border-b-2 -mb-px transition flex items-center gap-2 ${
              activeTab === "balance"
                ? "border-blue-600 text-blue-700 bg-blue-50/40 rounded-t-lg"
                : "border-transparent text-slate-500 hover:text-slate-800"
            }`}
          >
            <span>Bảng cân đối tồn kho 3 cấp ({totalStocksCount})</span>
          </button>

          <button
            type="button"
            onClick={() => setActiveTab("ledger")}
            className={`px-4 py-3 text-sm font-bold border-b-2 -mb-px transition flex items-center gap-2 ${
              activeTab === "ledger"
                ? "border-blue-600 text-blue-700 bg-blue-50/40 rounded-t-lg"
                : "border-transparent text-slate-500 hover:text-slate-800"
            }`}
          >
            <span>Sổ kho điện tử ({totalLedgerCount})</span>
          </button>
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

