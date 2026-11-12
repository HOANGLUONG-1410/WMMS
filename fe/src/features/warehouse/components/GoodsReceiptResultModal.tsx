import { poStatusLabel } from "../../../lib/statusLabels";
import type { GoodsReceiptResult } from "../types";

interface GoodsReceiptResultModalProps {
  result: GoodsReceiptResult | null;
  onClose: () => void;
}

const fmtNum = (n?: number | null) => (n ?? 0).toLocaleString("vi-VN");
const fmtDate = (v?: string | null) => {
  if (!v) return "—";
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleDateString("vi-VN");
};

/** Kết quả sau khi xác nhận nhập kho: tồn sau nhập và các đơn được tự phân bổ theo ETD. */
export default function GoodsReceiptResultModal({ result, onClose }: GoodsReceiptResultModalProps) {
  if (!result) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-4xl max-h-[90vh] flex flex-col overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 bg-emerald-50 flex items-center justify-between shrink-0">
          <h2 className="text-base font-bold text-emerald-900">
            Nhập kho thành công — phiếu <span className="font-mono">{result.grnCode}</span>
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-lg font-bold leading-none px-2 py-1 rounded-lg hover:bg-slate-200/60 transition"
          >
            ✕
          </button>
        </div>

        <div className="p-6 space-y-5 overflow-y-auto">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
            <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
              <div className="text-xs text-slate-500">Đơn mua hàng</div>
              <div className="font-mono font-bold text-slate-900 mt-1">{result.poCode}</div>
            </div>
            <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
              <div className="text-xs text-slate-500">Trạng thái PO sau nhập</div>
              <div className="font-semibold text-slate-900 mt-1">{poStatusLabel(result.poStatus)}</div>
            </div>
            <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
              <div className="text-xs text-slate-500">Thời điểm xác nhận</div>
              <div className="font-semibold text-slate-900 mt-1">
                {new Date(result.committedAt).toLocaleString("vi-VN")}
              </div>
            </div>
            <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
              <div className="text-xs text-slate-500">Đơn được tự phân bổ</div>
              <div className="font-bold text-blue-700 mt-1">{result.allocatedOrderCount} đơn</div>
            </div>
          </div>

          <div>
            <h3 className="text-sm font-bold text-slate-900 mb-2">Tồn kho sau khi nhập</h3>
            <div className="border border-slate-200 rounded-xl overflow-x-auto">
              <table className="w-full text-left border-collapse text-sm">
                <thead>
                  <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                    <th className="py-2.5 px-4">Vật tư</th>
                    <th className="py-2.5 px-3 text-right">Nhận lần này</th>
                    <th className="py-2.5 px-3 text-right">Còn lại trên PO</th>
                    <th className="py-2.5 px-3 text-right">Tồn vật lý</th>
                    <th className="py-2.5 px-3 text-right">Đã giữ chỗ</th>
                    <th className="py-2.5 px-3 text-right">Khả dụng</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {result.lines.map((l) => (
                    <tr key={l.materialId}>
                      <td className="py-2.5 px-4">
                        <div className="font-semibold text-slate-900">{l.materialName}</div>
                        <div className="font-mono text-xs text-slate-500">{l.materialCode}</div>
                      </td>
                      <td className="py-2.5 px-3 text-right font-bold text-emerald-700">
                        +{fmtNum(l.receivedQty)} {l.unitOfMeasure}
                      </td>
                      <td className="py-2.5 px-3 text-right text-slate-700">{fmtNum(l.remainingQty)}</td>
                      <td className="py-2.5 px-3 text-right">{fmtNum(l.physicalQtyAfter)}</td>
                      <td className="py-2.5 px-3 text-right">{fmtNum(l.allocatedQtyAfter)}</td>
                      <td className="py-2.5 px-3 text-right font-semibold">{fmtNum(l.availableQtyAfter)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div>
            <h3 className="text-sm font-bold text-slate-900 mb-2">
              Tự động phân bổ cho đơn hàng đang chờ (ưu tiên ETD sớm nhất)
            </h3>
            {result.allocations.length === 0 ? (
              <div className="text-sm text-slate-500">Không có đơn hàng nào đang chờ vật tư này.</div>
            ) : (
              <div className="border border-slate-200 rounded-xl overflow-x-auto">
                <table className="w-full text-left border-collapse text-sm">
                  <thead>
                    <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                      <th className="py-2.5 px-4">Số PO khách</th>
                      <th className="py-2.5 px-3">Không gian</th>
                      <th className="py-2.5 px-3">ETD</th>
                      <th className="py-2.5 px-3">Vật tư</th>
                      <th className="py-2.5 px-3 text-right">Phân bổ thêm</th>
                      <th className="py-2.5 px-3 text-right">Đã giữ / Cần</th>
                      <th className="py-2.5 px-3 text-right">Còn thiếu</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200">
                    {result.allocations.map((a) => (
                      <tr key={a.allocationId}>
                        <td className="py-2.5 px-4 font-mono">{a.poNumber}</td>
                        <td className="py-2.5 px-3 font-mono">{a.spaceKey}</td>
                        <td className="py-2.5 px-3">{fmtDate(a.etdDate)}</td>
                        <td className="py-2.5 px-3 font-mono">{a.materialCode}</td>
                        <td className="py-2.5 px-3 text-right font-bold text-blue-700">
                          +{fmtNum(a.allocatedNowQty)}
                        </td>
                        <td className="py-2.5 px-3 text-right">
                          {fmtNum(a.reservedQty)} / {fmtNum(a.requiredQty)}
                        </td>
                        <td
                          className={`py-2.5 px-3 text-right font-semibold ${
                            a.shortageQty > 0 ? "text-rose-600" : "text-emerald-700"
                          }`}
                        >
                          {fmtNum(a.shortageQty)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {result.readyOrderPoNumbers.length > 0 && (
              <div className="mt-3 px-4 py-3 rounded-lg border border-emerald-200 bg-emerald-50 text-sm text-emerald-800">
                <strong>Đã đủ vật tư:</strong> {result.readyOrderPoNumbers.join(", ")}
              </div>
            )}
          </div>
        </div>

        <div className="px-6 py-4 border-t border-slate-200 bg-slate-50 flex justify-end shrink-0">
          <button
            type="button"
            onClick={onClose}
            className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}
