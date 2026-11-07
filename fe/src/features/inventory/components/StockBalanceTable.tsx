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
  { key: "ALL", label: "Tất cả vật tư" },
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
    <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
      {/* Table Top Bar: Title + Category Filter + Search Input */}
      <div className="px-6 py-4 border-b border-slate-200 space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="flex items-center gap-2.5">
            <h2 className="text-base font-bold text-slate-900">
              Tồn kho vật tư theo thời gian thực
            </h2>
            {isFetching && !isLoading && (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-600 border border-blue-200">
                <span className="w-2 h-2 rounded-full border border-blue-600 border-t-transparent animate-spin" />
                Đang đồng bộ...
              </span>
            )}
          </div>

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
                className="w-full pl-3.5 pr-8 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-slate-800 placeholder:text-slate-400 shadow-2xs"
              />
              {searchInput && (
                <button
                  type="button"
                  onClick={handleClearSearch}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-sm font-bold px-1"
                  title="Xóa tìm kiếm"
                >
                  ×
                </button>
              )}
            </div>

            <button
              type="submit"
              disabled={isFetching}
              className="px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-60 rounded-lg shadow-xs transition whitespace-nowrap"
            >
              Tìm kiếm
            </button>
          </form>
        </div>

        {/* Category Filter Pills & Safety Stock Filter */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-0.5">
          <div className="flex flex-wrap items-center gap-2">
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
                  className={`px-3.5 py-1.5 rounded-lg text-sm font-semibold transition border ${
                    isSelected
                      ? "bg-blue-600 text-white border-blue-600 shadow-xs"
                      : "bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100"
                  }`}
                >
                  {tab.label}
                </button>
              );
            })}
          </div>

          <label className="inline-flex items-center gap-2 text-sm font-medium text-slate-700 cursor-pointer select-none">
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
              className="rounded border-slate-300 text-rose-600 focus:ring-rose-500 w-4 h-4"
            />
            <span>Chỉ hiện vật tư dưới ngưỡng an toàn</span>
          </label>
        </div>
      </div>

      {/* Real-time Material Inventory Balance Table */}
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500">
              <th className="py-3.5 px-6 bg-[#F8FAFC]">Mã vật tư</th>
              <th className="py-3.5 px-4 bg-[#F8FAFC]">Nhóm</th>
              <th className="py-3.5 px-4 bg-[#F8FAFC]">Mô tả</th>
              <th className="py-3.5 px-3 bg-[#F8FAFC]">ĐVT</th>
              <th className="py-3.5 px-4 bg-slate-100/80 text-slate-800 font-bold">
                1. Tổng tồn vật lý
              </th>
              <th className="py-3.5 px-4 bg-amber-50/90 text-amber-900 font-bold">
                2. Tồn đã phân bổ (Giữ chỗ)
              </th>
              <th className="py-3.5 px-4 bg-emerald-50/90 text-emerald-900 font-bold">
                3. Tồn khả dụng (Tự do)
              </th>
              <th className="py-3.5 px-4 bg-[#F8FAFC]">
                Đơn hàng đang giữ chỗ
              </th>
              <th className="py-3.5 px-6 bg-[#F8FAFC] text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 text-sm">
            {isFetching ? (
              <tr>
                <td colSpan={9} className="py-12 text-center text-slate-400">
                  <div className="inline-block w-6 h-6 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                  <p className="text-sm font-medium text-slate-500">
                    Đang tải tồn kho 3 cấp...
                  </p>
                </td>
              </tr>
            ) : stocks.length === 0 ? (
              <tr>
                <td colSpan={9} className="py-12 text-center text-slate-500 text-sm">
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
                    className="hover:bg-slate-50/70 transition-colors"
                  >
                    {/* Material Code */}
                    <td className="py-4 px-6 font-mono text-sm font-bold text-slate-900 whitespace-nowrap">
                      {item.materialCode}
                    </td>

                    {/* Category */}
                    <td className="py-4 px-4 text-slate-600 whitespace-nowrap">
                      {getCategoryDisplay(item.materialGroup)}
                    </td>

                    {/* Description + DimensionSpec */}
                    <td className="py-4 px-4">
                      <div className="font-semibold text-slate-900">
                        {item.materialName}
                        {item.dimensionSpec ? (
                          <span className="text-slate-500 font-normal">
                            {" "}
                            ({item.dimensionSpec})
                          </span>
                        ) : null}
                      </div>
                      <div className="flex items-center gap-2 mt-1 text-xs text-slate-500">
                        <span>
                          Tồn an toàn:{" "}
                          {formatQuantityWithUnit(item.safetyStockQty, item.unitOfMeasure)}
                        </span>
                        {item.fscCertified && (
                          <span className="px-2 py-0.5 rounded bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold text-xs">
                            FSC 100%
                          </span>
                        )}
                      </div>
                    </td>

                    {/* UoM */}
                    <td className="py-4 px-3 text-slate-600 whitespace-nowrap">
                      {formatDisplayUom(item.unitOfMeasure)}
                    </td>

                    {/* 1. Total Physical Stock */}
                    <td className="py-4 px-4 bg-slate-50/40 font-extrabold text-slate-900 whitespace-nowrap">
                      {formatQuantityWithUnit(item.physicalQty, item.unitOfMeasure)}
                    </td>

                    {/* 2. Allocated Stock (Reserved) */}
                    <td className="py-4 px-4 bg-amber-50/50 font-extrabold text-amber-700 whitespace-nowrap">
                      {formatQuantityWithUnit(item.allocatedQty, item.unitOfMeasure)}
                    </td>

                    {/* 3. Available Stock (Free) */}
                    <td
                      className={`py-4 px-4 font-extrabold whitespace-nowrap ${
                        isShortageOrLow
                          ? "bg-rose-50/70 text-rose-600"
                          : "bg-emerald-50/50 text-emerald-700"
                      }`}
                    >
                      {formatQuantityWithUnit(item.availableQty, item.unitOfMeasure)}
                      {item.availableQty <= 0 && (
                        <span className="ml-1.5 text-xs font-bold text-rose-600">
                          (Thiếu hàng)
                        </span>
                      )}
                      {item.availableQty > 0 && isShortageOrLow && (
                        <span className="ml-1.5 text-xs font-bold text-rose-600">
                          (Tồn thấp)
                        </span>
                      )}
                    </td>

                    {/* Active Allocations (Order Spaces) */}
                    <td className="py-4 px-4">
                      {allocations.length > 0 ? (
                        <div className="flex flex-wrap items-center gap-1.5">
                          {allocations.map((alloc, idx) => (
                            <button
                              key={`${alloc.spaceKey}-${idx}`}
                              type="button"
                              onClick={() => onInspectStockLedger(item)}
                              className="px-2.5 py-1 rounded-md bg-blue-50 hover:bg-blue-100 text-blue-700 border border-blue-200 font-mono text-xs font-bold transition whitespace-nowrap"
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
                        <span className="text-xs text-slate-400 italic">
                          Không có khóa giữ chỗ (Tự do)
                        </span>
                      )}
                    </td>

                    {/* Ledger Action Button */}
                    <td className="py-4 px-6 text-right whitespace-nowrap">
                      <button
                        type="button"
                        onClick={() => onInspectStockLedger(item)}
                        className="px-3.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 rounded-lg text-xs font-semibold shadow-2xs transition"
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

      {/* Thanh Phân trang */}
      <div className="px-6 py-4 border-t border-slate-200 bg-[#F8FAFC] flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-sm text-slate-600">
        <div className="flex items-center gap-3">
          <span>
            Hiển thị <strong className="text-slate-900">{startItem}</strong>–
            <strong className="text-slate-900">{endItem}</strong> trong tổng số{" "}
            <strong className="text-slate-900">{totalCount}</strong> vật tư
          </span>

          <div className="flex items-center gap-2 ml-2 border-l border-slate-200 pl-3">
            <span className="text-slate-500 text-xs">Hiển thị:</span>
            <select
              value={pageSize}
              onChange={(e) =>
                onChangeFilter({
                  ...filter,
                  pageSize: Number(e.target.value),
                  page: 1,
                })
              }
              className="px-2.5 py-1 bg-white border border-slate-300 rounded-lg text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value={10}>10 dòng</option>
              <option value={20}>20 dòng</option>
              <option value={50}>50 dòng</option>
            </select>
          </div>
        </div>

        <div className="flex items-center gap-1.5">
          <button
            type="button"
            disabled={pageNumber <= 1 || isFetching}
            onClick={() =>
              onChangeFilter({
                ...filter,
                page: Math.max(1, pageNumber - 1),
              })
            }
            className="px-3 py-1.5 text-sm font-medium text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition"
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
                    <span className="px-1 text-slate-400 text-sm">...</span>
                  )}
                  <button
                    type="button"
                    disabled={isFetching}
                    onClick={() => onChangeFilter({ ...filter, page: p })}
                    className={`w-8 h-8 text-sm font-semibold rounded-lg transition ${
                      p === pageNumber
                        ? "bg-blue-600 text-white shadow-xs"
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
            className="px-3 py-1.5 text-sm font-medium text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition"
          >
            Sau
          </button>
        </div>
      </div>
    </div>
  );
}

