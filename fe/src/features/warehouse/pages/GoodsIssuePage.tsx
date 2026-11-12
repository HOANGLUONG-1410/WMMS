import { useState } from "react";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useSession } from "../../auth/store/SessionContext";
import { ROLES } from "../../../constants/roles";
import { allocationStatusLabel } from "../../../lib/statusLabels";
import { useOrderAllocations, useOrdersForIssue } from "../hooks/useWarehouse";

const fmtNum = (n?: number | null) => (n ?? 0).toLocaleString("vi-VN");
const fmtDate = (v?: string | null) => {
  if (!v) return "—";
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleDateString("vi-VN");
};

export default function GoodsIssuePage() {
  const { currentUser } = useSession();
  // Quản trị viên chỉ được xem, không được xác nhận xuất kho.
  const readOnly = currentUser?.activeRole === ROLES.ADMIN;
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [selectedOrderId, setSelectedOrderId] = useState<number | null>(null);
  const [notice, setNotice] = useState(false);

  const { data: orders = [], isLoading, isError } = useOrdersForIssue(search);
  const { data: allocations = [], isLoading: loadingAlloc, isError: allocError } =
    useOrderAllocations(selectedOrderId);

  const selectedOrder = orders.find((o) => o.orderId === selectedOrderId) ?? null;
  const totalReserved = allocations.reduce((s, a) => s + a.reservedQty, 0);
  const totalIssued = allocations.reduce((s, a) => s + a.issuedQty, 0);
  const totalShortage = allocations.reduce((s, a) => s + a.shortageQty, 0);

  return (
    <WmmsAppShell mode="warehouse">
      <div className="max-w-[1400px] mx-auto space-y-6">
        <div>
          <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
            Xuất kho cấp phát vật tư
          </h1>
          <p className="text-sm text-slate-500 mt-1">
            Chọn đơn hàng để xem vật tư đã được giữ chỗ (cấp phát) và chuẩn bị xuất kho bàn giao xuống xưởng.
          </p>
        </div>

        {readOnly && (
          <div className="px-4 py-3 rounded-xl border bg-slate-50 border-slate-200 text-slate-700 text-sm font-semibold">
            Chế độ chỉ xem: tài khoản Quản trị không được thực hiện xuất kho.
          </div>
        )}

        {notice && (
          <div className="px-4 py-3 rounded-xl border bg-amber-50 border-amber-200 text-amber-800 text-sm font-semibold flex items-center justify-between">
            <span>
              Chức năng xác nhận xuất kho chưa được hỗ trợ ở hệ thống phía sau. Hiện chỉ xem được thông tin cấp phát.
            </span>
            <button type="button" onClick={() => setNotice(false)} className="ml-3 font-bold text-slate-400 hover:text-slate-600">
              ✕
            </button>
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
          <div className="lg:col-span-2 bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-200 space-y-3">
              <h2 className="text-base font-bold text-slate-900">Đơn hàng</h2>
              <div className="flex gap-2">
                <input
                  type="text"
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  onKeyDown={(e) => e.key === "Enter" && setSearch(searchInput.trim())}
                  placeholder="Tìm số PO, khách hàng..."
                  className="flex-1 px-3.5 py-2 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                <button
                  type="button"
                  onClick={() => setSearch(searchInput.trim())}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg transition"
                >
                  Tìm
                </button>
              </div>
            </div>
            <div className="max-h-[560px] overflow-y-auto divide-y divide-slate-200">
              {isLoading && <div className="py-10 text-center text-slate-400 text-sm">Đang tải đơn hàng...</div>}
              {isError && <div className="py-10 text-center text-rose-600 text-sm">Không tải được danh sách đơn hàng.</div>}
              {!isLoading && !isError && orders.length === 0 && (
                <div className="py-10 text-center text-slate-500 text-sm">Không có đơn hàng nào.</div>
              )}
              {orders.map((o) => (
                <button
                  key={o.orderId}
                  type="button"
                  onClick={() => setSelectedOrderId(o.orderId)}
                  className={`w-full text-left px-5 py-4 transition hover:bg-slate-50 ${
                    o.orderId === selectedOrderId ? "bg-blue-50 border-l-4 border-blue-600" : "border-l-4 border-transparent"
                  }`}
                >
                  <div className="font-mono font-bold text-slate-900">{o.poNumber}</div>
                  <div className="text-sm text-slate-700 mt-0.5">{o.customerName}</div>
                  <div className="text-xs text-slate-500 mt-0.5">
                    ETD: {fmtDate(o.etdDate)} · Không gian {o.orderSpaceKey}
                  </div>
                </button>
              ))}
            </div>
          </div>

          <div className="lg:col-span-3 bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
            {!selectedOrder ? (
              <div className="h-full min-h-[300px] flex items-center justify-center text-slate-400 text-sm px-6 text-center">
                Chọn một đơn hàng ở bên trái để xem vật tư đã cấp phát.
              </div>
            ) : (
              <>
                <div className="px-6 py-4 border-b border-slate-200 bg-slate-50">
                  <h2 className="text-base font-bold text-slate-900">
                    Cấp phát vật tư cho đơn <span className="font-mono">{selectedOrder.poNumber}</span>
                  </h2>
                  <p className="text-sm text-slate-500 mt-0.5">Khách hàng: {selectedOrder.customerName}</p>
                </div>

                <div className="grid grid-cols-3 gap-3 p-6 pb-0 text-sm">
                  <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
                    <div className="text-xs text-slate-500">Đã giữ chỗ</div>
                    <div className="font-bold text-slate-900 mt-1">{fmtNum(totalReserved)}</div>
                  </div>
                  <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
                    <div className="text-xs text-slate-500">Đã xuất kho</div>
                    <div className="font-bold text-emerald-700 mt-1">{fmtNum(totalIssued)}</div>
                  </div>
                  <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
                    <div className="text-xs text-slate-500">Còn thiếu</div>
                    <div className={`font-bold mt-1 ${totalShortage > 0 ? "text-rose-600" : "text-slate-900"}`}>
                      {fmtNum(totalShortage)}
                    </div>
                  </div>
                </div>

                <div className="overflow-x-auto mt-4">
                  <table className="w-full text-left border-collapse">
                    <thead>
                      <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                        <th className="py-3 px-5">Sản phẩm</th>
                        <th className="py-3 px-3 text-right">Cần</th>
                        <th className="py-3 px-3 text-right">Giữ chỗ</th>
                        <th className="py-3 px-3 text-right">Đã xuất</th>
                        <th className="py-3 px-3 text-right">Thiếu</th>
                        <th className="py-3 px-5">Trạng thái</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200 text-sm">
                      {loadingAlloc && (
                        <tr>
                          <td colSpan={6} className="py-8 text-center text-slate-400">
                            Đang tải thông tin cấp phát...
                          </td>
                        </tr>
                      )}
                      {allocError && (
                        <tr>
                          <td colSpan={6} className="py-8 text-center text-rose-600">
                            Không tải được thông tin cấp phát.
                          </td>
                        </tr>
                      )}
                      {!loadingAlloc && !allocError && allocations.length === 0 && (
                        <tr>
                          <td colSpan={6} className="py-8 text-center text-slate-500">
                            Đơn hàng này chưa có vật tư nào được cấp phát.
                          </td>
                        </tr>
                      )}
                      {allocations.map((a) => (
                        <tr key={a.allocationId}>
                          <td className="py-3 px-5">
                            <div className="font-semibold text-slate-900">{a.productName}</div>
                            <div className="font-mono text-xs text-slate-500">{a.productSku}</div>
                          </td>
                          <td className="py-3 px-3 text-right">{fmtNum(a.requiredQty)}</td>
                          <td className="py-3 px-3 text-right">{fmtNum(a.reservedQty)}</td>
                          <td className="py-3 px-3 text-right text-emerald-700">{fmtNum(a.issuedQty)}</td>
                          <td
                            className={`py-3 px-3 text-right font-semibold ${
                              a.shortageQty > 0 ? "text-rose-600" : "text-slate-700"
                            }`}
                          >
                            {fmtNum(a.shortageQty)}
                          </td>
                          <td className="py-3 px-5 whitespace-nowrap">{allocationStatusLabel(a.allocationStatus)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {!readOnly && (
                <div className="px-6 py-4 border-t border-slate-200 flex justify-end">
                  <button
                    type="button"
                    onClick={() => setNotice(true)}
                    className="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg shadow-xs transition"
                  >
                    Xác nhận xuất kho
                  </button>
                </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>
    </WmmsAppShell>
  );
}
