import { useState } from "react";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useSession } from "../../auth/store/SessionContext";
import { ROLES } from "../../../constants/roles";
import { prStatusLabel } from "../../../lib/statusLabels";
import { usePurchaseRequests } from "../hooks/usePurchaseRequests";
import { prStatusBadgeClass } from "../types";
import PrDetailModal from "../components/PrDetailModal";

const STATUS_OPTIONS = ["PENDING_APPROVAL", "APPROVED", "REJECTED", "PO_ISSUED"];
// Nhân viên Thu mua chỉ được xem PR đã duyệt (backend ép quy tắc này).
const PURCHASING_STATUS_OPTIONS = ["APPROVED", "PO_ISSUED"];

const fmtDate = (v?: string | null) => {
  if (!v) return "—";
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleDateString("vi-VN");
};

export default function PurchaseRequestPage() {
  const { currentUser } = useSession();
  const purchasingOnly = currentUser?.activeRole === ROLES.PURCHASING;
  const statusOptions = purchasingOnly ? PURCHASING_STATUS_OPTIONS : STATUS_OPTIONS;

  const [searchInput, setSearchInput] = useState("");
  const [keyword, setKeyword] = useState("");
  const [status, setStatus] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const invalidRange = !!fromDate && !!toDate && fromDate > toDate;

  const { data, isLoading, isError } = usePurchaseRequests({
    status,
    fromDate: invalidRange ? undefined : fromDate,
    toDate: invalidRange ? undefined : toDate,
    searchKeyword: keyword,
    pageNumber,
    pageSize,
  });

  const items = data?.items ?? [];
  const total = data?.totalCount ?? 0;
  const startItem = total === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endItem = Math.min(pageNumber * pageSize, total);

  const applySearch = () => {
    setKeyword(searchInput.trim());
    setPageNumber(1);
  };

  return (
    <WmmsAppShell mode="purchasing">
      <div className="max-w-[1400px] mx-auto space-y-6">
        {toast && (
          <div className="px-4 py-3 rounded-xl border bg-emerald-50 border-emerald-200 text-emerald-800 text-sm font-semibold flex items-center justify-between">
            <span>{toast}</span>
            <button
              type="button"
              onClick={() => setToast(null)}
              className="text-slate-400 hover:text-slate-600 ml-3 font-bold"
            >
              ✕
            </button>
          </div>
        )}

        <div>
          <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
            Yêu cầu mua hàng gộp theo ngày
          </h1>
          <p className="text-sm text-slate-500 mt-1">
            Theo dõi các phiếu yêu cầu mua vật tư được gom theo ngày từ phần thiếu hụt tồn kho.
            {currentUser?.activeRole === ROLES.OWNER
              ? " Chủ xưởng xem chi tiết để duyệt hoặc từ chối."
              : purchasingOnly
              ? " Bộ phận Thu mua chỉ xem được các phiếu đã được Chủ xưởng duyệt."
              : ""}
          </p>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-col lg:flex-row lg:items-end gap-3">
            <div className="relative flex-1 min-w-[200px]">
              <label className="block text-xs font-semibold text-slate-500 mb-1">Tìm kiếm</label>
              <input
                type="text"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && applySearch()}
                placeholder="Mã phiếu, tên vật tư..."
                className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1">Trạng thái</label>
              <select
                value={status}
                onChange={(e) => {
                  setStatus(e.target.value);
                  setPageNumber(1);
                }}
                className="w-full lg:w-44 px-3 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="">Tất cả</option>
                {statusOptions.map((s) => (
                  <option key={s} value={s}>
                    {prStatusLabel(s)}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1">Từ ngày</label>
              <input
                type="date"
                value={fromDate}
                onChange={(e) => {
                  setFromDate(e.target.value);
                  setPageNumber(1);
                }}
                className="px-3 py-2 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1">Đến ngày</label>
              <input
                type="date"
                value={toDate}
                onChange={(e) => {
                  setToDate(e.target.value);
                  setPageNumber(1);
                }}
                className="px-3 py-2 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <button
              type="button"
              onClick={applySearch}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition"
            >
              Lọc
            </button>
          </div>
          {invalidRange && (
            <div className="px-6 py-2 text-xs text-rose-600 bg-rose-50 border-b border-rose-100">
              Ngày bắt đầu không được lớn hơn ngày kết thúc.
            </div>
          )}

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-6">Mã phiếu</th>
                  <th className="py-3.5 px-4">Ngày gom</th>
                  <th className="py-3.5 px-4 text-right">Số vật tư</th>
                  <th className="py-3.5 px-4 text-right">Tổng thiếu hụt</th>
                  <th className="py-3.5 px-4 text-right">Tổng đề xuất đặt</th>
                  <th className="py-3.5 px-4">Người lập</th>
                  <th className="py-3.5 px-4">Trạng thái</th>
                  <th className="py-3.5 px-6 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm">
                {isLoading && (
                  <tr>
                    <td colSpan={8} className="py-10 text-center text-slate-400">
                      Đang tải danh sách phiếu yêu cầu mua hàng...
                    </td>
                  </tr>
                )}
                {isError && (
                  <tr>
                    <td colSpan={8} className="py-10 text-center text-rose-600">
                      Không tải được danh sách phiếu yêu cầu mua hàng.
                    </td>
                  </tr>
                )}
                {!isLoading && !isError && items.length === 0 && (
                  <tr>
                    <td colSpan={8} className="py-10 text-center text-slate-500">
                      Không có phiếu yêu cầu mua hàng nào khớp với bộ lọc.
                    </td>
                  </tr>
                )}
                {items.map((pr) => (
                  <tr key={pr.prId} className="hover:bg-slate-50/80 transition-colors">
                    <td className="py-4 px-6 font-mono font-bold text-slate-900 whitespace-nowrap">{pr.prCode}</td>
                    <td className="py-4 px-4 text-slate-600 whitespace-nowrap">{fmtDate(pr.aggregationDate)}</td>
                    <td className="py-4 px-4 text-right text-slate-800">{pr.itemsCount}</td>
                    <td className="py-4 px-4 text-right font-semibold text-rose-600">
                      {pr.totalShortageQty.toLocaleString("vi-VN")}
                    </td>
                    <td className="py-4 px-4 text-right font-bold text-slate-900">
                      {pr.totalSuggestedOrderQty.toLocaleString("vi-VN")}
                    </td>
                    <td className="py-4 px-4 text-slate-600 whitespace-nowrap">{pr.creatorName || "Hệ thống"}</td>
                    <td className="py-4 px-4 whitespace-nowrap">
                      <span
                        className={`inline-flex px-2.5 py-1 text-xs font-bold uppercase rounded-full border ${prStatusBadgeClass(pr.prStatus)}`}
                      >
                        {prStatusLabel(pr.prStatus)}
                      </span>
                    </td>
                    <td className="py-4 px-6 text-right whitespace-nowrap">
                      <button
                        type="button"
                        onClick={() => setSelectedId(pr.prId)}
                        className="px-3.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg shadow-2xs transition"
                      >
                        {currentUser?.activeRole === ROLES.OWNER && pr.prStatus === "PENDING_APPROVAL"
                          ? "Xem & duyệt"
                          : "Xem chi tiết"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="px-6 py-3.5 border-t border-slate-200 bg-[#F8FAFC] flex flex-col sm:flex-row items-center justify-between gap-3 text-sm text-slate-600">
            <span>
              Hiển thị {startItem}–{endItem} / {total} phiếu
            </span>
            <div className="flex items-center gap-2">
              <select
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setPageNumber(1);
                }}
                className="px-2 py-1.5 text-sm bg-white border border-slate-300 rounded-lg"
              >
                {[10, 20, 50].map((n) => (
                  <option key={n} value={n}>
                    {n} / trang
                  </option>
                ))}
              </select>
              <button
                type="button"
                disabled={!data?.hasPreviousPage}
                onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                className="px-3 py-1.5 bg-white border border-slate-300 rounded-lg font-semibold disabled:opacity-40"
              >
                Trước
              </button>
              <span className="font-semibold">
                {pageNumber}/{Math.max(1, data?.totalPages ?? 1)}
              </span>
              <button
                type="button"
                disabled={!data?.hasNextPage}
                onClick={() => setPageNumber((p) => p + 1)}
                className="px-3 py-1.5 bg-white border border-slate-300 rounded-lg font-semibold disabled:opacity-40"
              >
                Sau
              </button>
            </div>
          </div>
        </div>
      </div>

      <PrDetailModal
        prId={selectedId}
        onClose={() => setSelectedId(null)}
        onSuccess={(msg) => setToast(msg)}
      />
    </WmmsAppShell>
  );
}
