import { Fragment, useState, type FormEvent } from "react";
import { DEFAULT_INVENTORY_STOCKS } from "../data/defaultInventoryData";
import type {
  InventoryLedgerFilterDto,
  InventoryStockDto,
  InventoryTransactionDto,
} from "../types";

interface StockLedgerTableProps {
  transactions: InventoryTransactionDto[];
  allMaterials?: InventoryStockDto[];
  totalCount: number;
  totalPages?: number;
  isLoading: boolean;
  isFetching?: boolean;
  filter: InventoryLedgerFilterDto;
  onChangeFilter: (nextFilter: InventoryLedgerFilterDto) => void;
  onResetFilter: () => void;
}

export default function StockLedgerTable({
  transactions,
  allMaterials,
  totalCount,
  totalPages: serverTotalPages,
  isLoading,
  isFetching = false,
  filter,
  onChangeFilter,
  onResetFilter,
}: StockLedgerTableProps) {
  const [searchInput, setSearchInput] = useState(filter.search || "");
  const materialOptions =
    allMaterials && allMaterials.length > 0 ? allMaterials : DEFAULT_INVENTORY_STOCKS;
  const formatQty = (val: number, uom: string) => {
    const upper = (uom || "").toUpperCase();
    const formatted =
      upper === "M3" || upper === "M³" || upper === "M2"
        ? val.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
        : val.toLocaleString("en-US", { maximumFractionDigits: 2 });
    const displayUom = upper === "M3" ? "m³" : upper === "M2" ? "m²" : uom;
    return `${formatted} ${displayUom}`;
  };

  const getTxTypeMeta = (txType: string) => {
    const upper = (txType || "").toUpperCase();
    if (upper === "INITIAL_IMPORT") {
      return {
        badgeClass: "bg-emerald-100 text-emerald-800 border-emerald-300",
        qtyClass: "text-emerald-700",
        prefix: "+",
        label: "Tồn đầu kỳ",
      };
    }
    if (upper === "IN_RECEIPT") {
      return {
        badgeClass: "bg-blue-100 text-blue-800 border-blue-300",
        qtyClass: "text-blue-700",
        prefix: "+",
        label: "Nhập kho GRN",
      };
    }
    if (upper === "OUT_ISSUE") {
      return {
        badgeClass: "bg-amber-100 text-amber-800 border-amber-300",
        qtyClass: "text-rose-600",
        prefix: "−",
        label: "Xuất kho sản xuất",
      };
    }
    return {
      badgeClass: "bg-purple-100 text-purple-800 border-purple-300",
      qtyClass: "text-purple-700",
      prefix: "±",
      label: "Kiểm kê / Điều chỉnh",
    };
  };

  const getCategoryLabel = (group: string) => {
    const upper = (group || "").toUpperCase();
    if (upper === "LUMBER") return "Gỗ nguyên khối";
    if (upper === "HARDWARE") return "Phụ kiện";
    if (upper === "FINISH_PAINT") return "Sơn & hoàn thiện";
    if (upper === "PACKAGING") return "Bao bì & đóng gói";
    return group;
  };

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

  return (
    <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
      {/* Header & Filter Bar */}
      <div className="p-6 border-b border-slate-200 space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-base font-bold text-slate-900">
                Sổ kho điện tử
              </h2>
              {isFetching && !isLoading && (
                <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-600 border border-blue-200">
                  <span className="w-2 h-2 rounded-full border border-blue-600 border-t-transparent animate-spin" />
                  Đang đồng bộ...
                </span>
              )}
            </div>
            <p className="text-sm text-slate-500 mt-0.5">
              Nhật ký bất biến về nhập tồn đầu kỳ, phiếu nhập kho (GRN), xuất vật tư xuống xưởng và điều chỉnh tồn kho.
            </p>
          </div>
        </div>

        {/* Bộ lọc Sổ kho */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3 pt-1">
          {/* Tìm theo mã phiếu / vật tư */}
          <form onSubmit={handleSearchSubmit}>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 mb-1.5">
              Tìm mã phiếu / vật tư
            </label>
            <div className="flex items-center gap-1.5">
              <div className="relative flex-1">
                <input
                  type="text"
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  placeholder="VD: INIT-STOCK, WOOD..."
                  className="w-full pl-3 pr-7 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
                />
                {searchInput && (
                  <button
                    type="button"
                    onClick={handleClearSearch}
                    className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-sm font-bold px-1"
                    title="Xóa tìm kiếm"
                  >
                    ×
                  </button>
                )}
              </div>
              <button
                type="submit"
                disabled={isFetching}
                className="px-3 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-60 rounded-lg shadow-xs transition shrink-0"
              >
                Tìm
              </button>
            </div>
          </form>

          {/* Lọc theo Vật tư */}
          <div>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 mb-1.5">
              Vật tư
            </label>
            <select
              value={filter.materialId || ""}
              onChange={(e) =>
                onChangeFilter({
                  ...filter,
                  materialId: e.target.value ? Number(e.target.value) : undefined,
                  page: 1,
                })
              }
              className="w-full px-3 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
            >
              <option value="">Tất cả vật tư</option>
              {materialOptions.map((m) => (
                <option key={m.materialId} value={m.materialId}>
                  {m.materialCode} — {m.materialName}
                </option>
              ))}
            </select>
          </div>

          {/* Lọc theo Loại giao dịch */}
          <div>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 mb-1.5">
              Loại giao dịch
            </label>
            <select
              value={filter.txType || "ALL"}
              onChange={(e) =>
                onChangeFilter({ ...filter, txType: e.target.value, page: 1 })
              }
              className="w-full px-3 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
            >
              <option value="ALL">Tất cả loại giao dịch</option>
              <option value="INITIAL_IMPORT">Nhập tồn đầu kỳ</option>
              <option value="IN_RECEIPT">Nhập kho mua hàng</option>
              <option value="OUT_ISSUE">Xuất kho sản xuất</option>
              <option value="ADJUST">Điều chỉnh kiểm kê</option>
            </select>
          </div>

          {/* Từ ngày */}
          <div>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 mb-1.5">
              Từ ngày
            </label>
            <input
              type="date"
              value={filter.startDate || ""}
              onChange={(e) =>
                onChangeFilter({ ...filter, startDate: e.target.value || undefined, page: 1 })
              }
              className="w-full px-3 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
            />
          </div>

          {/* Đến ngày + Reset */}
          <div className="flex items-end gap-2">
            <div className="flex-1">
              <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 mb-1.5">
                Đến ngày
              </label>
              <input
                type="date"
                value={filter.endDate || ""}
                onChange={(e) =>
                  onChangeFilter({ ...filter, endDate: e.target.value || undefined, page: 1 })
                }
                className="w-full px-3 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
              />
            </div>
            <button
              type="button"
              onClick={() => {
                setSearchInput("");
                onResetFilter();
              }}
              className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg transition shrink-0 shadow-2xs"
              title="Xóa bộ lọc"
            >
              Đặt lại
            </button>
          </div>
        </div>
      </div>

      {/* Bảng dữ liệu Sổ kho */}
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
              <th className="py-3.5 px-6">Mã giao dịch</th>
              <th className="py-3.5 px-4">Ngày &amp; giờ thực hiện</th>
              <th className="py-3.5 px-4">Loại giao dịch</th>
              <th className="py-3.5 px-4">Mã chứng từ tham chiếu</th>
              <th className="py-3.5 px-4">Mã vật tư</th>
              <th className="py-3.5 px-4">Mô tả &amp; nhóm vật tư</th>
              <th className="py-3.5 px-3 text-center">ĐVT</th>
              <th className="py-3.5 px-6 text-right">Biến động số lượng</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 text-sm">
            {isFetching ? (
              <tr>
                <td colSpan={8} className="py-12 text-center text-slate-400">
                  <div className="inline-block w-6 h-6 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                  <p className="text-sm font-medium text-slate-500">
                    Đang tải sổ kho điện tử...
                  </p>
                </td>
              </tr>
            ) : transactions.length === 0 ? (
              <tr>
                <td colSpan={8} className="py-10 text-center text-slate-500 text-sm">
                  Không tìm thấy giao dịch sổ kho nào khớp với điều kiện lọc hiện tại.
                </td>
              </tr>
            ) : (
              transactions.map((tx) => {
                const meta = getTxTypeMeta(tx.txType);
                return (
                  <tr
                    key={tx.transactionId}
                    className="hover:bg-slate-50/80 transition-colors"
                  >
                    <td className="py-4 px-6 font-mono text-sm font-bold text-slate-600 whitespace-nowrap">
                      #TX-{String(tx.transactionId).padStart(4, "0")}
                    </td>
                    <td className="py-4 px-4 text-slate-600 whitespace-nowrap">
                      {new Date(tx.createdAt).toLocaleString("en-GB", {
                        day: "2-digit",
                        month: "2-digit",
                        year: "numeric",
                        hour: "2-digit",
                        minute: "2-digit",
                      })}
                    </td>
                    <td className="py-4 px-4 whitespace-nowrap">
                      <span
                        className={`inline-block px-2.5 py-1 text-xs font-bold rounded-full border ${meta.badgeClass}`}
                      >
                        {meta.label}
                      </span>
                    </td>
                    <td className="py-4 px-4 whitespace-nowrap">
                      <span className="px-2.5 py-1 rounded-md bg-slate-100 text-slate-800 border border-slate-300 font-mono text-xs font-bold">
                        {tx.referenceCode || "N/A"}
                      </span>
                    </td>
                    <td className="py-4 px-4 font-mono text-sm font-bold text-slate-900 whitespace-nowrap">
                      {tx.materialCode}
                    </td>
                    <td className="py-4 px-4">
                      <div className="font-semibold text-slate-900">{tx.materialName}</div>
                      <div className="text-xs text-slate-500 mt-0.5">
                        {getCategoryLabel(tx.materialGroup)}
                      </div>
                    </td>
                    <td className="py-4 px-3 text-center text-slate-600 font-medium whitespace-nowrap">
                      {tx.unitOfMeasure === "M3"
                        ? "m³"
                        : tx.unitOfMeasure === "M2"
                          ? "m²"
                          : tx.unitOfMeasure}
                    </td>
                    <td className={`py-4 px-6 text-right font-extrabold whitespace-nowrap ${meta.qtyClass}`}>
                      {meta.prefix}
                      {formatQty(tx.quantity, tx.unitOfMeasure)}
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
            <strong className="text-slate-900">{totalCount}</strong> giao dịch
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

