import { useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { authApi } from "../api/authApi";
import { getErrorMessage } from "../store/SessionContext";

interface FeedbackMessage {
  type: "error" | "success";
  text: string;
}

export interface ResetPasswordModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function ResetPasswordModal({
  isOpen,
  onClose,
}: ResetPasswordModalProps) {
  const [recoveryEmail, setRecoveryEmail] = useState("");
  const [recoveryFeedback, setRecoveryFeedback] =
    useState<FeedbackMessage | null>(null);

  const forgotPasswordMutation = useMutation({
    mutationFn: (email: string) => authApi.forgotPassword(email),
    onSuccess: (apiRes) => {
      setRecoveryFeedback({
        type: "success",
        text:
          apiRes?.message ||
          apiRes?.data?.message ||
          "Hướng dẫn đặt lại mật khẩu đã được gửi tới email của bạn.",
      });
    },
    onError: (err: unknown) => {
      setRecoveryFeedback({
        type: "error",
        text: getErrorMessage(err),
      });
    },
  });

  if (!isOpen) return null;

  const handleSendRecoveryToken = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!recoveryEmail.trim()) {
      setRecoveryFeedback({
        type: "error",
        text: "Vui lòng nhập email nhân viên đã đăng ký.",
      });
      return;
    }

    setRecoveryFeedback(null);
    forgotPasswordMutation.mutate(recoveryEmail.trim());
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[490px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        <div className="px-6 py-4 flex items-center justify-between border-b border-slate-200">
          <h2 className="text-base font-bold text-slate-900">
            Đặt lại mật khẩu cá nhân
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-xl font-bold leading-none px-1 cursor-pointer"
            aria-label="Đóng hộp thoại"
          >
            ×
          </button>
        </div>

        <form
          onSubmit={handleSendRecoveryToken}
          className="px-6 py-5 space-y-4"
        >
          <p className="text-sm text-slate-600 leading-relaxed">
            Nhập email nhân viên đã đăng ký. Hệ thống sẽ tạo một mã
            xác thực dùng một lần và gửi vào hộp thư công ty của bạn.
          </p>

          <div>
            <label
              htmlFor="recovery-email-input"
              className="block text-sm font-medium text-slate-700 mb-1.5"
            >
              Địa chỉ email nhân viên
            </label>
            <input
              id="recovery-email-input"
              type="email"
              value={recoveryEmail}
              onChange={(e) => setRecoveryEmail(e.target.value)}
              placeholder="nhanvien@nguyenkhoa.vn"
              className="w-full px-3.5 py-2 text-sm text-slate-800 bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
            />
          </div>

          {recoveryFeedback && (
            <div
              className={`px-3.5 py-2.5 rounded-lg text-sm ${
                recoveryFeedback.type === "error"
                  ? "bg-rose-50 text-rose-700 border border-rose-200"
                  : "bg-emerald-50 text-emerald-700 border border-emerald-200"
              }`}
            >
              {recoveryFeedback.text}
            </div>
          )}

          <div className="pt-2 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              disabled={forgotPasswordMutation.isPending}
              className="px-4 py-2 text-sm font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition disabled:opacity-60 cursor-pointer"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={forgotPasswordMutation.isPending}
              className="px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-xs transition disabled:opacity-60 inline-flex items-center gap-2 cursor-pointer"
            >
              {forgotPasswordMutation.isPending && (
                <span className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
              )}
              {forgotPasswordMutation.isPending
                ? "Đang gửi..."
                : "Gửi mã khôi phục"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
