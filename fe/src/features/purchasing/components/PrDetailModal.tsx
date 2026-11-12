import { Fragment, useEffect, useState } from "react";
import { useSession } from "../../auth/store/SessionContext";
import { ROLES } from "../../../constants/roles";
import { prStatusLabel } from "../../../lib/statusLabels";
import { useApprovePr, usePurchaseRequestDetail } from "../hooks/usePurchaseRequests";
import { prStatusBadgeClass } from "../types";
import RejectPrModal from "./RejectPrModal";

interface PrDetailModalProps {
  prId: number | null;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

const fmtDate = (v?: string | null) => {
  if (!v) return "—";
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleDateString("vi-VN");
};
const fmtNum = (n?: number | null) => (n ?? 0).toLocaleString("vi-VN");

/** Chi tiết PR: thông tin chung, vật tư thiếu hụt, đơn nguồn; Chủ xưởng có thể duyệt / từ chối. */
export default function PrDetailModal({ prId, onClose, onSuccess }: PrDetailModalProps) {
  const { currentUser } = useSession();
  const { data: pr, isLoading, isError } = usePurchaseRequestDetail(prId);
  const approveMutation = useApprovePr();
  const [notes, setNotes] = useState("");
  const [expanded, setExpanded] = useState<number | null>(null);
  const [rejectOpen, setRejectOpen] = useState(false);

  useEffect(() => {
    setNotes("");
    setExpanded(null);
    setRejectOpen(false);
  }, [prId]);

  if (prId === null) return null;

  const canReview = currentUser?.activeRole === ROLES.OWNER && pr?.prStatus === "PENDING_APPROVAL";

  const handleApprove = async () => {
    try {
      const res = await approveMutation.mutateAsync({ id: prId, notes: notes.trim() || undefined });
      onSuccess(`Đã duyệt phiếu yêu cầu mua hàng ${res.prCode} và chuyển sang bộ phận Thu mua.`);
      onClose();
    } catch {
      // Lỗi đã được hiển thị bởi bộ xử lý lỗi toàn cục.
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-5xl max-h-[90vh] flex flex-col overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between shrink-0">
          <h2 className="text-base font-bold text-slate-900">
            Chi tiết phiếu yêu cầu mua hàng {pr ? <span className="font-mono">{pr.prCode}</span> : ""}
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
          {isLoading && <div className="py-10 text-center text-slate-400 text-sm">Đang tải chi tiết phiếu...</div>}
          {isError && (
            <div className="py-10 text-center text-rose-600 text-sm">Không tải được chi tiết phiếu yêu cầu mua hàng.</div>
          )}

          {pr && (
            <>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
                <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
                  <div className="text-xs text-slate-500">Trạng thái</div>
                  <span
                    className={`inline-flex mt-1 px-2.5 py-0.5 text-xs font-bold uppercase rounded-full border ${prStatusBadgeClass(pr.prStatus)}`}
                  >
                    {prStatusLabel(pr.prStatus)}
                  </span>
                </div>
                <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
                  <div className="text-xs text-slate-500">Ngày gom</div>
                  <div className="font-semibold text-slate-900 mt-1">{fmtDate(pr.aggregationDate)}</div>
                </div>
                <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
                  <div className="text-xs text-slate-500">Người lập</div>
                  <div className="font-semibold text-slate-900 mt-1">{pr.creatorName || "Hệ thống"}</div>
                </div>
                <div className="p-3 bg-slate-50 rounded-lg border border-slate-200">
                  <div className="text-xs text-slate-500">Người duyệt / ngày duyệt</div>
                  <div className="font-semibold text-slate-900 mt-1">
                    {pr.approverName ? `${pr.approverName} · ${fmtDate(pr.reviewedAt)}` : "—"}
                  </div>
                </div>
              </div>

              {pr.prStatus === "REJECTED" && pr.rejectionReason && (
                <div className="px-4 py-3 rounded-lg border border-rose-200 bg-rose-50 text-sm text-rose-800">
                  <strong>Lý do từ chối:</strong> {pr.rejectionReason}
                </div>
              )}

              {pr.contributingSpaceKeys.length > 0 && (
                <div className="text-sm text-slate-600">
                  <span className="text-slate-500">Không gian đơn hàng liên quan:</span>{" "}
                  {pr.contributingSpaceKeys.map((k) => (
                    <span key={k} className="inline-block mr-1.5 px-2 py-0.5 bg-slate-100 rounded font-mono text-xs">
                      {k}
                    </span>
                  ))}
                </div>
              )}

              <div className="border border-slate-200 rounded-xl overflow-hidden">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                      <th className="py-3 px-4">Vật tư</th>
                      <th className="py-3 px-3 text-right">Tồn khả dụng</th>
                      <th className="py-3 px-3 text-right">Thiếu hụt</th>
                      <th className="py-3 px-3 text-right">Đề xuất đặt</th>
                      <th className="py-3 px-3">ETD sớm nhất</th>
                      <th className="py-3 px-4 text-right">Đơn nguồn</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200 text-sm">
                    {pr.items.map((it) => (
                      <Fragment key={it.prItemId}>
                        <tr className="hover:bg-slate-50/80">
                          <td className="py-3 px-4">
                            <div className="font-semibold text-slate-900">{it.materialName}</div>
                            <div className="font-mono text-xs text-slate-500">{it.materialCode}</div>
                          </td>
                          <td className="py-3 px-3 text-right text-slate-700">
                            {fmtNum(it.availableStockQty)} {it.unitOfMeasure}
                          </td>
                          <td className="py-3 px-3 text-right font-semibold text-rose-600">
                            {fmtNum(it.totalShortageQty)} {it.unitOfMeasure}
                          </td>
                          <td className="py-3 px-3 text-right font-bold text-slate-900">
                            {fmtNum(it.suggestedOrderQty)} {it.unitOfMeasure}
                          </td>
                          <td className="py-3 px-3 text-slate-600 whitespace-nowrap">{fmtDate(it.earliestEtd)}</td>
                          <td className="py-3 px-4 text-right">
                            <button
                              type="button"
                              onClick={() => setExpanded(expanded === it.prItemId ? null : it.prItemId)}
                              className="text-xs font-semibold text-blue-600 hover:text-blue-800"
                            >
                              {it.sourceOrders.length} đơn {expanded === it.prItemId ? "▲" : "▼"}
                            </button>
                          </td>
                        </tr>
                        {expanded === it.prItemId && (
                          <tr className="bg-slate-50/70">
                            <td colSpan={6} className="px-4 py-3">
                              <table className="w-full text-xs">
                                <thead>
                                  <tr className="text-slate-500 text-left">
                                    <th className="py-1 pr-3">Số PO khách</th>
                                    <th className="py-1 pr-3">Không gian</th>
                                    <th className="py-1 pr-3">Sản phẩm</th>
                                    <th className="py-1 pr-3">ETD</th>
                                    <th className="py-1 text-right">Thiếu</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {it.sourceOrders.map((s) => (
                                    <tr key={`${s.orderId}-${s.orderItemId}`} className="border-t border-slate-200">
                                      <td className="py-1.5 pr-3 font-mono">{s.poNumber}</td>
                                      <td className="py-1.5 pr-3 font-mono">{s.spaceKey || "—"}</td>
                                      <td className="py-1.5 pr-3">{s.productSku || "—"}</td>
                                      <td className="py-1.5 pr-3">{fmtDate(s.etdDate)}</td>
                                      <td className="py-1.5 text-right font-semibold">{fmtNum(s.shortageQty)}</td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </td>
                          </tr>
                        )}
                      </Fragment>
                    ))}
                  </tbody>
                </table>
              </div>

              {canReview && (
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                    Ghi chú phê duyệt (không bắt buộc)
                  </label>
                  <textarea
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                    rows={2}
                    maxLength={500}
                    className="w-full px-3.5 py-2.5 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
                  />
                </div>
              )}
            </>
          )}
        </div>

        <div className="px-6 py-4 border-t border-slate-200 bg-slate-50 flex items-center justify-end gap-2.5 shrink-0">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg transition"
          >
            Đóng
          </button>
          {canReview && (
            <>
              <button
                type="button"
                onClick={() => setRejectOpen(true)}
                disabled={approveMutation.isPending}
                className="px-4 py-2.5 bg-white hover:bg-rose-50 text-rose-600 border border-rose-300 text-sm font-semibold rounded-lg transition disabled:opacity-60"
              >
                Từ chối
              </button>
              <button
                type="button"
                onClick={handleApprove}
                disabled={approveMutation.isPending}
                className="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-60"
              >
                {approveMutation.isPending ? "Đang duyệt..." : "Duyệt phiếu"}
              </button>
            </>
          )}
        </div>
      </div>

      <RejectPrModal
        prId={rejectOpen ? prId : null}
        prCode={pr?.prCode}
        onClose={() => setRejectOpen(false)}
        onSuccess={(msg) => {
          onSuccess(msg);
          onClose();
        }}
      />
    </div>
  );
}
