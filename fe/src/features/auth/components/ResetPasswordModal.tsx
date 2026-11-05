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
    <div className="fixed inset-0 z-50 bg-slate-950/70 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[490px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        <div className="px-6 pt-5 pb-3 flex items-center justify-between border-b border-slate-100">
          <h2 className="text-sm font-bold text-slate-900">
            Đặt lại mật khẩu cá nhân
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1"
            aria-label="Đóng hộp thoại"
          >
            ×
          </button>
        </div>

        <form
          onSubmit={handleSendRecoveryToken}
          className="px-6 py-4 space-y-4"
        >
          <p className="text-xs text-slate-500 leading-relaxed">
            Nhập email nhân viên đã đăng ký. Hệ thống sẽ tạo một mã
            xác thực dùng một lần và gửi vào hộp thư công ty của bạn.
          </p>

          <div>
            <label
              htmlFor="recovery-email-input"
              className="block text-xs font-semibold text-slate-700 mb-1.5"
            >
              Địa chỉ email nhân viên
            </label>
            <input
              id="recovery-email-input"
              type="email"
              value={recoveryEmail}
              onChange={(e) => setRecoveryEmail(e.target.value)}
              placeholder="nhanvien@nguyenkhoa.vn"
              className="w-full px-3 py-2 text-xs text-slate-800 bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {recoveryFeedback && (
            <div
              className={`px-3 py-2 rounded text-xs ${
                recoveryFeedback.type === "error"
                  ? "bg-red-50 text-red-700 border border-red-200"
                  : "bg-emerald-50 text-emerald-700 border border-emerald-200"
              }`}
            >
              {recoveryFeedback.text}
            </div>
          )}

          <div className="pt-2 flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              disabled={forgotPasswordMutation.isPending}
              className="px-3.5 py-2 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50 transition disabled:opacity-60"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={forgotPasswordMutation.isPending}
              className="px-4 py-2 text-xs font-semibold text-white bg-[#2563eb] hover:bg-blue-700 rounded-md shadow-sm transition disabled:opacity-60 inline-flex items-center gap-2"
            >
              {forgotPasswordMutation.isPending && (
                <svg
                  className="animate-spin h-3.5 w-3.5 text-white"
                  xmlns="http://www.w3.org/2000/svg"
                  fill="none"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                >
                  <circle
                    className="opacity-25"
                    cx="12"
                    cy="12"
                    r="10"
                    stroke="currentColor"
                    strokeWidth="4"
                  />
                  <path
                    className="opacity-75"
                    fill="currentColor"
                    d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                  />
                </svg>
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
