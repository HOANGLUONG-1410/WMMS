import { Fragment, useState, type FormEvent } from "react";
import type { InventoryStockDto, InventoryStockFilterDto } from "../types";

interface StockBalanceTableProps {
  stocks: InventoryStockDto[];
  totalCount: number;
  totalPages?: number;
  isLoading: boolean;
  isFetching?: boolean;
  filter: InventoryStockFilterDto;
  onChangeFilter: (nextFilter: InventoryStockFilterDto) => void;
  onInspectStockLedger: (item: InventoryStockDto) => void;
}

const CATEGORY_TABS = [
  { key: "ALL", label: "Tất cả vật tư (14)" },
  { key: "LUMBER", label: "Gỗ nguyên khối" },
  { key: "HARDWARE", label: "Phụ kiện" },
  { key: "FINISH_PAINT", label: "Sơn & hoàn thiện" },
  { key: "PACKAGING", label: "Bao bì" },
];

export default function StockBalanceTable({
  stocks,
  totalCount,
  totalPages: serverTotalPages,
  isLoading,
  isFetching = false,
  filter,
  onChangeFilter,
  onInspectStockLedger,
}: StockBalanceTableProps) {
  const [searchInput, setSearchInput] = useState(filter.search || "");

  const activeCategory = (filter.category || filter.group || "ALL").toUpperCase();
  const pageNumber = filter.page || 1;
  const pageSize = filter.pageSize || 10;
  const totalPages = Math.max(
    1,
    serverTotalPages || Math.ceil(totalCount / pageSize),
  );
  const startItem = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endItem = Math.min(pageNumber * pageSize, totalCount);

  const handleSearchSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    onChangeFilter({
      ...filter,
      search: searchInput.trim() || undefined,
      page: 1,
    });
  };

  const handleClearSearch = () => {
    setSearchInput("");
    onChangeFilter({
      ...filter,
      search: undefined,
      page: 1,
    });
  };

  const formatDisplayUom = (uom: string) => {
    const upper = (uom || "").toUpperCase();
    if (upper === "M3" || upper === "M³") return "m³";
    if (upper === "M2" || upper === "M²") return "m²";
    if (upper === "PCS") return "Cái";
    if (upper === "LITER") return "Lít";
    if (upper === "SHEET") return "Tấm";
    if (upper === "SET") return "Bộ";
    return uom;
  };

  const formatQuantityWithUnit = (qty: number, uom: string) => {
    const upper = (uom || "").toUpperCase();
    const isCubicOrArea = upper === "M3" || upper === "M³" || upper === "M2";
    const formattedNumber = isCubicOrArea
      ? qty.toLocaleString("en-US", {
          minimumFractionDigits: 2,
          maximumFractionDigits: 2,
        })
      : qty.toLocaleString("en-US", {
          maximumFractionDigits: 2,
        });
    return `${formattedNumber} ${formatDisplayUom(uom)}`;
  };

  const getCategoryDisplay = (group: string) => {
    const upper = (group || "").toUpperCase();
    if (upper === "LUMBER") return "Gỗ nguyên khối";
    if (upper === "HARDWARE") return "Phụ kiện";
    if (upper === "FINISH_PAINT") return "Sơn & hoàn thiện";
    if (upper === "PACKAGING") return "Bao bì";
    return group;
  };

  return (
    <div className="bg-white rounded-xl border border-slate-200 shadow-2xs overflow-hidden">
      {/* Table Top Bar: Title + Category Filter + Search Input (Field #3) */}
      <div className="px-5 py-4 border-b border-slate-200 space-y-3">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="flex items-center gap-2.5">
            <h2 className="text-sm font-extrabold text-slate-900">
              Tồn kho vật tư theo thời gian thực
            </h2>
            <span className="px-2 py-0.5 text-[11px] font-bold rounded-full bg-slate-100 text-slate-700 border border-slate-200">
              {totalCount} SKU
            </span>
            {isFetching && !isLoading && (
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[10px] font-medium bg-blue-50 text-blue-600 border border-blue-200">
                <span className="w-2 h-2 rounded-full border border-blue-600 border-t-transparent animate-spin" />
                Đang gọi máy chủ...
              </span>
            )}
          </div>

          {/* Field #3: Form Tìm kiếm có nút bấm xác nhận giống UserManagementPage */}
          <form
            onSubmit={handleSearchSubmit}
            className="flex items-center gap-2 w-full sm:w-auto"
          >
            <div className="relative w-full sm:w-72">
              <input
                type="text"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder="Tìm theo mã vật tư hoặc mô tả..."
                className="w-full pl-3.5 pr-7 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 text-slate-800 placeholder:text-slate-400"
              />
              {searchInput && (
                <button
                  type="button"
                  onClick={handleClearSearch}
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-xs font-bold px-1"
                  title="Xóa tìm kiếm"
                >
                  ×
                </button>
              )}
            </div>

            <button
              type="submit"
              disabled={isFetching}
              className="px-3.5 py-1.5 text-xs font-semibold text-white bg-[#2563eb] hover:bg-blue-700 disabled:opacity-60 rounded-md shadow-sm transition whitespace-nowrap"
            >
              Tìm kiếm
            </button>
          </form>
        </div>

        {/* Category Filter Pills & Safety Stock Filter */}
        <div className="flex flex-wrap items-center justify-between gap-2 pt-0.5">
          <div className="flex flex-wrap items-center gap-1.5">
            {CATEGORY_TABS.map((tab) => {
              const isSelected = activeCategory === tab.key;
              return (
                <button
                  key={tab.key}
                  type="button"
                  onClick={() =>
                    onChangeFilter({
                      ...filter,
                      category: tab.key === "ALL" ? undefined : tab.key,
                      page: 1,
                    })
                  }
                  className={`px-3 py-1 rounded-md text-xs font-semibold transition border ${
                    isSelected
                      ? "bg-slate-900 text-white border-slate-900 shadow-2xs"
                      : "bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100"
                  }`}
                >
                  {tab.label}
                </button>
              );
            })}
          </div>

          <label className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-600 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={Boolean(filter.belowSafetyStock)}
              onChange={(e) =>
                onChangeFilter({
                  ...filter,
                  belowSafetyStock: e.target.checked ? true : undefined,
                  page: 1,
                })
              }
              className="rounded border-slate-300 text-rose-600 focus:ring-rose-500"
            />
            <span>Chỉ hiện vật tư dưới ngưỡng an toàn (Below Safety Buffer)</span>
          </label>
        </div>
      </div>

      {/* Field #4: Real-time Material Inventory Balance Table */}
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-500">
              <th className="py-3 px-5 bg-slate-50/60">Mã vật tư</th>
              <th className="py-3 px-4 bg-slate-50/60">Nhóm</th>
              <th className="py-3 px-4 bg-slate-50/60">Mô tả</th>
              <th className="py-3 px-3 bg-slate-50/60">ĐVT</th>
              <th className="py-3 px-4 bg-slate-100/70 text-slate-700 font-bold">
                1. Tổng tồn vật lý
              </th>
              <th className="py-3 px-4 bg-amber-50/90 text-amber-900 font-bold">
                2. Tồn đã phân bổ (Giữ chỗ)
              </th>
              <th className="py-3 px-4 bg-emerald-50/90 text-emerald-900 font-bold">
                3. Tồn khả dụng (Tự do)
              </th>
              <th className="py-3 px-4 bg-slate-50/60">
                Phân bổ đang hoạt động (Order Space)
              </th>
              <th className="py-3 px-5 bg-slate-50/60 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 text-xs">
            {isFetching ? (
              <tr>
                <td colSpan={9} className="py-12 text-center text-slate-400">
                  <div className="inline-block w-6 h-6 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                  <p className="text-xs font-medium text-slate-500">
                    Đang tải tồn kho 3 cấp từ máy chủ WMMS...
                  </p>
                </td>
              </tr>
            ) : stocks.length === 0 ? (
              <tr>
                <td colSpan={9} className="py-12 text-center text-slate-400">
                  Không có vật tư nào khớp với bộ lọc hiện tại.
                </td>
              </tr>
            ) : (
              stocks.map((item) => {
                const isShortageOrLow =
                  item.availableQty <= 0 ||
                  item.isBelowSafetyStock ||
                  item.availableQty < item.safetyStockQty;

                const allocations = item.activeAllocationsPreview || [];

                return (
                  <tr
                    key={item.materialId}
                    className="hover:bg-slate-50/60 transition-colors"
                  >
                    {/* Material Code */}
                    <td className="py-3.5 px-5 font-mono text-xs font-semibold text-slate-900">
                      {item.materialCode}
                    </td>

                    {/* Category */}
                    <td className="py-3.5 px-4 text-slate-600">
                      {getCategoryDisplay(item.materialGroup)}
                    </td>

                    {/* Description + DimensionSpec */}
                    <td className="py-3.5 px-4">
                      <div className="font-medium text-slate-800">
                        {item.materialName}
                        {item.dimensionSpec ? (
                          <span className="text-slate-500 font-normal">
                            {" "}
                            ({item.dimensionSpec})
                          </span>
                        ) : null}
                      </div>
                      <div className="flex items-center gap-2 mt-0.5 text-[10px] text-slate-400">
                        <span>
                          Tồn an toàn:{" "}
                          {formatQuantityWithUnit(item.safetyStockQty, item.unitOfMeasure)}
                        </span>
                        {item.fscCertified && (
                          <span className="px-1.5 py-0.2 rounded bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold">
                            FSC 100%
                          </span>
                        )}
                      </div>
                    </td>

                    {/* UoM */}
                    <td className="py-3.5 px-3 text-slate-600">
                      {formatDisplayUom(item.unitOfMeasure)}
                    </td>

                    {/* 1. Total Physical Stock */}
                    <td className="py-3.5 px-4 bg-slate-50/40 font-extrabold text-slate-900">
                      {formatQuantityWithUnit(item.physicalQty, item.unitOfMeasure)}
                    </td>

                    {/* 2. Allocated Stock (Reserved) - Amber Shaded Column */}
                    <td className="py-3.5 px-4 bg-amber-50/50 font-extrabold text-[#d97706]">
                      {formatQuantityWithUnit(item.allocatedQty, item.unitOfMeasure)}
                    </td>

                    {/* 3. Available Stock (Free) - Green or Rose Shaded Column */}
                    <td
                      className={`py-3.5 px-4 font-extrabold ${
                        isShortageOrLow
                          ? "bg-rose-50/70 text-rose-600"
                          : "bg-emerald-50/50 text-emerald-700"
                      }`}
                    >
                      {formatQuantityWithUnit(item.availableQty, item.unitOfMeasure)}
                      {item.availableQty <= 0 && (
                        <span className="ml-1 text-[11px] font-bold text-rose-600">
                          (Thiếu hàng)
                        </span>
                      )}
                      {item.availableQty > 0 && isShortageOrLow && (
                        <span className="ml-1 text-[10px] font-bold text-rose-600">
                          (Tồn thấp)
                        </span>
                      )}
                    </td>

                    {/* Active Allocations (Order Spaces) */}
                    <td className="py-3.5 px-4">
                      {allocations.length > 0 ? (
                        <div className="flex flex-wrap items-center gap-1.5">
                          {allocations.map((alloc, idx) => (
                            <button
                              key={`${alloc.spaceKey}-${idx}`}
                              type="button"
                              onClick={() => onInspectStockLedger(item)}
                              className="px-2.5 py-0.5 rounded bg-blue-50 hover:bg-blue-100 text-blue-700 border border-blue-200 font-mono text-[11px] font-bold transition"
                              title={`Bấm để xem chi tiết giữ chỗ của ${alloc.spaceKey}`}
                            >
                              {alloc.spaceKey} (
                              {formatQuantityWithUnit(
                                alloc.reservedQty,
                                item.unitOfMeasure,
                              )}
                              )
                            </button>
                          ))}
                        </div>
                      ) : (
                        <span className="text-[11px] text-slate-400 italic">
                          Không có khóa giữ chỗ (Tự do)
                        </span>
                      )}
                    </td>

                    {/* Field #5: Ledger Action Button */}
                    <td className="py-3.5 px-5 text-right">
                      <button
                        type="button"
                        onClick={() => onInspectStockLedger(item)}
                        className="px-3 py-1 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 rounded text-xs font-semibold shadow-2xs transition"
                      >
                        Sổ kho
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Thanh Phân trang (Pagination Footer — đồng bộ giao diện với UserManagementPage) */}
      <div className="px-5 py-3 border-t border-slate-200 bg-slate-50/40 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs text-slate-600">
        {/* Thống kê dòng hiển thị & chọn PageSize */}
        <div className="flex items-center gap-3">
          <span>
            Hiển thị <strong className="text-slate-800">{startItem}</strong> đến{" "}
            <strong className="text-slate-800">{endItem}</strong> trên{" "}
            <strong className="text-slate-800">{totalCount}</strong> vật tư
          </span>

          <div className="flex items-center gap-1.5 ml-2 border-l border-slate-200 pl-3">
            <span className="text-slate-500 text-[11px]">Số dòng/trang:</span>
            <select
              value={pageSize}
              onChange={(e) =>
                onChangeFilter({
                  ...filter,
                  pageSize: Number(e.target.value),
                  page: 1,
                })
              }
              className="px-2 py-1 bg-white border border-slate-300 rounded text-xs text-slate-700 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
          </div>
        </div>

        {/* Các nút bấm chuyển trang */}
        <div className="flex items-center gap-1">
          <button
            type="button"
            disabled={pageNumber <= 1 || isFetching}
            onClick={() =>
              onChangeFilter({
                ...filter,
                page: Math.max(1, pageNumber - 1),
              })
            }
            className="px-2.5 py-1 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition"
          >
            Trước
          </button>

          <div className="flex items-center gap-1 px-1">
            {Array.from({ length: totalPages }, (_, i) => i + 1)
              .filter(
                (p) =>
                  p === 1 ||
                  p === totalPages ||
                  Math.abs(p - pageNumber) <= 1,
              )
              .map((p, idx, arr) => (
                <Fragment key={p}>
                  {idx > 0 && arr[idx - 1] !== p - 1 && (
                    <span className="px-1 text-slate-400 text-xs">...</span>
                  )}
                  <button
                    type="button"
                    disabled={isFetching}
                    onClick={() => onChangeFilter({ ...filter, page: p })}
                    className={`w-7 h-7 text-xs font-semibold rounded transition ${
                      p === pageNumber
                        ? "bg-[#2563eb] text-white shadow-sm"
                        : "text-slate-700 bg-white border border-slate-300 hover:bg-slate-50"
                    }`}
                  >
                    {p}
                  </button>
                </Fragment>
              ))}
          </div>

          <button
            type="button"
            disabled={pageNumber >= totalPages || isFetching}
            onClick={() =>
              onChangeFilter({
                ...filter,
                page: Math.min(totalPages, pageNumber + 1),
              })
            }
            className="px-2.5 py-1 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition"
          >
            Sau
          </button>
        </div>
      </div>
    </div>
  );
}

