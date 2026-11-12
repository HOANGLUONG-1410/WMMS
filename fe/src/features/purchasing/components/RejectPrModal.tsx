import { useEffect, useState } from "react";
import { useRejectPr } from "../hooks/usePurchaseRequests";

interface RejectPrModalProps {
  prId: number | null;
  prCode?: string;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

const MAX_REASON = 500;

/** Hộp thoại từ chối PR: bắt buộc nhập lý do (tối đa 500 ký tự). */
export default function RejectPrModal({ prId, prCode, onClose, onSuccess }: RejectPrModalProps) {
  const rejectMutation = useRejectPr();
  const [reason, setReason] = useState("");

  useEffect(() => {
    if (prId !== null) setReason("");
  }, [prId]);

  if (prId === null) return null;

  const trimmed = reason.trim();
  const canSubmit = trimmed.length > 0 && trimmed.length <= MAX_REASON;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    try {
      await rejectMutation.mutateAsync({ id: prId, reason: trimmed });
      onSuccess(`Đã từ chối phiếu yêu cầu mua hàng ${prCode ?? ""} và chuyển trả Kế hoạch vật tư.`);
      onClose();
    } catch {
      // Lỗi đã được hiển thị bởi bộ xử lý lỗi toàn cục.
    }
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-md overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
          <h2 className="text-base font-bold text-slate-900">Từ chối phiếu yêu cầu mua hàng</h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-lg font-bold leading-none px-2 py-1 rounded-lg hover:bg-slate-200/60 transition"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <p className="text-sm text-slate-600">
            Phiếu <strong className="font-mono text-slate-900">{prCode}</strong> sẽ được trả về cho
            bộ phận Kế hoạch vật tư. Vui lòng nêu rõ lý do từ chối.
          </p>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1.5">
              Lý do từ chối <span className="text-rose-600">*</span>
            </label>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={4}
              maxLength={MAX_REASON}
              placeholder="Ví dụ: Số lượng đề xuất vượt nhu cầu thực tế..."
              className="w-full px-3.5 py-2.5 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500 resize-none"
            />
            <div className="text-xs text-slate-400 text-right mt-1">
              {reason.length}/{MAX_REASON}
            </div>
          </div>

          <div className="pt-1 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg transition"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={!canSubmit || rejectMutation.isPending}
              className="px-5 py-2.5 bg-rose-600 hover:bg-rose-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-60"
            >
              {rejectMutation.isPending ? "Đang xử lý..." : "Xác nhận từ chối"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
