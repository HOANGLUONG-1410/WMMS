import { useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { authApi } from "../api/authApi";

interface FeedbackMessage {
  type: "error" | "success";
  text: string;
}

export interface ResetPasswordModalProps {
  isOpen: boolean;
  onClose: () => void;
}

function getErrorMessage(error: unknown): string {
  if (error && typeof error === "object") {
    if (
      "errors" in error &&
      Array.isArray((error as { errors?: unknown }).errors)
    ) {
      const errs = (error as { errors: string[] }).errors;
      if (errs.length > 0 && errs[0]) return errs[0];
    }
    if (
      "message" in error &&
      typeof (error as { message?: unknown }).message === "string"
    ) {
      return (error as { message: string }).message;
    }
  }
  return "Unable to process password reset request. Please try again.";
}

/**
 * Modal 1.2: Reset Personal Password (UC-04 - Section III.1.2)
 * - Mô tả: "Enter your registered employee email. System will generate a single-use authentication token sent to your company inbox."
 * - Trường #1: Employee Email Address (employee@nguyenkhoa.vn).
 * - Nút #2: Cancel | Nút #3: Send Recovery Token.
 */
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
        text: apiRes?.message || apiRes?.data?.message || "Fail lòi lolololol",
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
        text: "Please enter your registered employee email address.",
      });
      return;
    }

    setRecoveryFeedback(null);
    forgotPasswordMutation.mutate(recoveryEmail.trim());
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-950/70 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[490px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        {/* Header Modal */}
        <div className="px-6 pt-5 pb-3 flex items-center justify-between border-b border-slate-100">
          <h2 className="text-sm font-bold text-slate-900">
            Reset Personal Password
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1"
            aria-label="Close modal"
          >
            ×
          </button>
        </div>

        {/* Nội dung Modal */}
        <form
          onSubmit={handleSendRecoveryToken}
          className="px-6 py-4 space-y-4"
        >
          <p className="text-xs text-slate-500 leading-relaxed">
            Enter your registered employee email. System will generate a
            single-use authentication token sent to your company inbox.
          </p>

          {/* Field #1: Employee Email Address */}
          <div>
            <label
              htmlFor="recovery-email-input"
              className="block text-xs font-semibold text-slate-700 mb-1.5"
            >
              Employee Email Address
            </label>
            <input
              id="recovery-email-input"
              type="email"
              value={recoveryEmail}
              onChange={(e) => setRecoveryEmail(e.target.value)}
              placeholder="employee@nguyenkhoa.vn"
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

          {/* Footer Buttons: Field #2 Cancel & Field #3 Send Recovery Token */}
          <div className="pt-2 flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              disabled={forgotPasswordMutation.isPending}
              className="px-3.5 py-2 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50 transition disabled:opacity-60"
            >
              Cancel
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
                ? "Sending..."
                : "Send Recovery Token"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
