import { useState } from "react";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useSession } from "../../auth/store/SessionContext";
import { ROLES } from "../../../constants/roles";
import { poStatusLabel } from "../../../lib/statusLabels";
import GoodsReceiptResultModal from "../components/GoodsReceiptResultModal";
import { useCommitGoodsReceipt, useOpenPurchaseOrders } from "../hooks/useWarehouse";
import type { GoodsReceiptResult } from "../types";

const fmtNum = (n?: number | null) => (n ?? 0).toLocaleString("vi-VN");
const fmtDate = (v?: string | null) => {
  if (!v) return "—";
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleDateString("vi-VN");
};

export default function GoodsReceiptPage() {
  const { currentUser } = useSession();
  // Quản trị viên chỉ được xem, không được xác nhận nhập kho.
  const readOnly = currentUser?.activeRole === ROLES.ADMIN;
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [selectedPoId, setSelectedPoId] = useState<number | null>(null);
  const [qtyByMaterial, setQtyByMaterial] = useState<Record<number, string>>({});
  const [note, setNote] = useState("");
  const [result, setResult] = useState<GoodsReceiptResult | null>(null);

  const { data: pos = [], isLoading, isError } = useOpenPurchaseOrders(search);
  const commitMutation = useCommitGoodsReceipt();

  const selectedPo = pos.find((p) => p.poId === selectedPoId) ?? null;

  const selectPo = (id: number) => {
    setSelectedPoId(id);
    setQtyByMaterial({});
    setNote("");
  };

  const lines = (selectedPo?.lines ?? []).map((l) => {
    const raw = qtyByMaterial[l.materialId] ?? "";
    const qty = raw === "" ? 0 : Number(raw);
    return { ...l, qty, invalid: Number.isNaN(qty) || qty < 0, over: qty > l.remainingQty };
  });
  const hasInvalid = lines.some((l) => l.invalid);
  const hasQty = lines.some((l) => l.qty > 0);
  const canSubmit = !readOnly && !!selectedPo && hasQty && !hasInvalid && !commitMutation.isPending;

  const handleCommit = async () => {
    if (!selectedPo || !canSubmit) return;
    try {
      const res = await commitMutation.mutateAsync({
        poId: selectedPo.poId,
        lines: lines.filter((l) => l.qty > 0).map((l) => ({ materialId: l.materialId, receivedQty: l.qty })),
        note: note.trim() || undefined,
      });
      setResult(res);
      setSelectedPoId(null);
      setQtyByMaterial({});
      setNote("");
    } catch {
      // Lỗi đã được hiển thị bởi bộ xử lý lỗi toàn cục.
    }
  };

  return (
    <WmmsAppShell mode="warehouse">
      <div className="max-w-[1400px] mx-auto space-y-6">
        <div>
          <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
            Nhập kho vật tư theo đơn mua hàng
          </h1>
          <p className="text-sm text-slate-500 mt-1">
            Chọn đơn mua hàng đang mở, nhập số lượng thực nhận rồi xác nhận. Hệ thống cập nhật tồn kho và tự động
            phân bổ vật tư cho các đơn hàng đang chờ theo ngày giao (ETD) sớm nhất.
          </p>
        </div>

        {readOnly && (
          <div className="px-4 py-3 rounded-xl border bg-slate-50 border-slate-200 text-slate-700 text-sm font-semibold">
            Chế độ chỉ xem: tài khoản Quản trị không được thực hiện nhập kho.
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
          {/* Danh sách PO đang mở */}
          <div className="lg:col-span-2 bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-200 space-y-3">
              <h2 className="text-base font-bold text-slate-900">Đơn mua hàng đang chờ nhập</h2>
              <div className="flex gap-2">
                <input
                  type="text"
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  onKeyDown={(e) => e.key === "Enter" && setSearch(searchInput.trim())}
                  placeholder="Tìm mã PO, nhà cung cấp..."
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
              {isLoading && <div className="py-10 text-center text-slate-400 text-sm">Đang tải đơn mua hàng...</div>}
              {isError && (
                <div className="py-10 text-center text-rose-600 text-sm">Không tải được danh sách đơn mua hàng.</div>
              )}
              {!isLoading && !isError && pos.length === 0 && (
                <div className="py-10 text-center text-slate-500 text-sm">Không có đơn mua hàng nào đang chờ nhập kho.</div>
              )}
              {pos.map((po) => (
                <button
                  key={po.poId}
                  type="button"
                  onClick={() => selectPo(po.poId)}
                  className={`w-full text-left px-5 py-4 transition hover:bg-slate-50 ${
                    po.poId === selectedPoId ? "bg-blue-50 border-l-4 border-blue-600" : "border-l-4 border-transparent"
                  }`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-mono font-bold text-slate-900">{po.poCode}</span>
                    <span className="text-xs font-bold px-2 py-0.5 rounded-full border bg-amber-50 text-amber-800 border-amber-200">
                      {poStatusLabel(po.poStatus)}
                    </span>
                  </div>
                  <div className="text-sm text-slate-700 mt-1">{po.supplierName}</div>
                  <div className="text-xs text-slate-500 mt-0.5">
                    Dự kiến về: {fmtDate(po.expectedArrivalDate)} · {po.lines.length} vật tư
                  </div>
                </button>
              ))}
            </div>
          </div>

          {/* Chi tiết nhập kho */}
          <div className="lg:col-span-3 bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
            {!selectedPo ? (
              <div className="h-full min-h-[300px] flex items-center justify-center text-slate-400 text-sm px-6 text-center">
                Chọn một đơn mua hàng ở bên trái để nhập số lượng thực nhận.
              </div>
            ) : (
              <>
                <div className="px-6 py-4 border-b border-slate-200 bg-slate-50">
                  <h2 className="text-base font-bold text-slate-900">
                    Phiếu nhập kho cho <span className="font-mono">{selectedPo.poCode}</span>
                  </h2>
                  <p className="text-sm text-slate-500 mt-0.5">Nhà cung cấp: {selectedPo.supplierName}</p>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full text-left border-collapse">
                    <thead>
                      <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                        <th className="py-3 px-5">Vật tư</th>
                        <th className="py-3 px-3 text-right">Đặt</th>
                        <th className="py-3 px-3 text-right">Đã nhận</th>
                        <th className="py-3 px-3 text-right">Còn lại</th>
                        {!readOnly && <th className="py-3 px-5 text-right">Thực nhận lần này</th>}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200 text-sm">
                      {lines.map((l) => (
                        <tr key={l.materialId}>
                          <td className="py-3 px-5">
                            <div className="font-semibold text-slate-900">{l.materialName}</div>
                            <div className="font-mono text-xs text-slate-500">{l.materialCode}</div>
                          </td>
                          <td className="py-3 px-3 text-right">
                            {fmtNum(l.orderedQty)} {l.unitOfMeasure}
                          </td>
                          <td className="py-3 px-3 text-right text-slate-600">{fmtNum(l.receivedQty)}</td>
                          <td className="py-3 px-3 text-right font-semibold">{fmtNum(l.remainingQty)}</td>
                          {!readOnly && (
                          <td className="py-3 px-5 text-right">
                            <input
                              type="number"
                              min={0}
                              step="any"
                              value={qtyByMaterial[l.materialId] ?? ""}
                              onChange={(e) =>
                                setQtyByMaterial((prev) => ({ ...prev, [l.materialId]: e.target.value }))
                              }
                              placeholder="0"
                              className={`w-28 px-3 py-2 text-sm text-right border rounded-lg focus:outline-none focus:ring-2 ${
                                l.invalid
                                  ? "border-rose-400 focus:ring-rose-500"
                                  : l.over
                                  ? "border-amber-400 focus:ring-amber-500"
                                  : "border-slate-300 focus:ring-blue-500"
                              }`}
                            />
                            {l.invalid && <div className="text-xs text-rose-600 mt-1">Số lượng không hợp lệ</div>}
                            {!l.invalid && l.over && (
                              <div className="text-xs text-amber-600 mt-1">Vượt phần còn lại trên PO</div>
                            )}
                          </td>
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {!readOnly && (
                <div className="px-6 py-4 border-t border-slate-200 space-y-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">Ghi chú nhập kho</label>
                    <textarea
                      value={note}
                      onChange={(e) => setNote(e.target.value)}
                      rows={2}
                      maxLength={500}
                      placeholder="Ví dụ: Giao thiếu 1 pallet, hàng bị ẩm..."
                      className="w-full px-3.5 py-2.5 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
                    />
                  </div>
                  <div className="flex items-center justify-end gap-2.5">
                    <button
                      type="button"
                      onClick={() => setSelectedPoId(null)}
                      className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg transition"
                    >
                      Bỏ chọn
                    </button>
                    <button
                      type="button"
                      onClick={handleCommit}
                      disabled={!canSubmit}
                      className="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-50"
                    >
                      {commitMutation.isPending ? "Đang xác nhận..." : "Xác nhận nhập kho"}
                    </button>
                  </div>
                </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>

      <GoodsReceiptResultModal result={result} onClose={() => setResult(null)} />
    </WmmsAppShell>
  );
}
