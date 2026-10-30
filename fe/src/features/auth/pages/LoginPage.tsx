import { useState, type FormEvent } from "react";
import { getErrorMessage } from "../store/SessionContext";
import { useLogin } from "../hooks/useLogin";
import ResetPasswordModal from "../components/ResetPasswordModal";

interface FeedbackMessage {
  type: "error" | "success";
  text: string;
}

/**
 * Màn hình III.1: Authorization (1.1 Login & 1.2 Forgot Password Modal)
 * Tuân thủ 100% thiết kế giao diện và bảng Field Description trong Report 3 (Section III.1):
 * - Nền tối `#0d1528`, thẻ trắng chính giữa bo góc.
 * - Tiêu đề: `WMMS PRODUCTION SUITE` | Phụ đề: `Nguyen Khoa Ha Nam Wood Factory`.
 * - Trường #1: `Work Email / Username` (mặc định `owner@nguyenkhoa.vn`).
 * - Trường #2: `Password` + Link #3 `Forgot password?` nằm cùng hàng bên phải nhãn Password (text-blue-600).
 * - Nút #4: `Sign In to Workspace` (bg-[#2563eb] text-white).
 * - Modal 1.2 (`Reset Personal Password`) được tách ra `ResetPasswordModal.tsx`.
 */
export default function LoginPage() {
  // State cho biểu mẫu Đăng nhập (Section III.1.1 - UC-01)
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [authMessage, setAuthMessage] = useState<FeedbackMessage | null>(null);

  // State cho Modal Quên mật khẩu (Section III.1.2 - UC-04)
  const [showForgotModal, setShowForgotModal] = useState(false);

  const loginMutation = useLogin();

  /**
   * UC-01: Xử lý sự kiện bấm nút "Sign In to Workspace"
   */
  const handleSignIn = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!identifier.trim() || !password.trim()) {
      setAuthMessage({
        type: "error",
        text: "Please enter both Work Email / Username and Password (MSG01).",
      });
      return;
    }

    setAuthMessage(null);
    loginMutation.mutate(
      {
        usernameOrEmail: identifier.trim(),
        password,
      },
      {
        onError: (err: unknown) => {
          setAuthMessage({
            type: "error",
            text: getErrorMessage(err),
          });
        },
      },
    );
  };

  return (
    <div className="min-h-screen w-full bg-[#0d1528] flex items-center justify-center p-4 relative select-none">
      {/* Thẻ Login chính giữa (Section III.1.1) */}
      <div className="w-full max-w-[380px] bg-white rounded-xl shadow-2xl px-7 py-8 border border-slate-100">
        {/* Tiêu đề hệ thống */}
        <div className="text-center mb-6">
          <h1 className="text-[17px] font-extrabold tracking-tight text-slate-900 uppercase">
            WMMS PRODUCTION SUITE
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            Nguyen Khoa Ha Nam Wood Factory
          </p>
        </div>

        {/* Thông báo phản hồi nếu có */}
        {authMessage && (
          <div
            className={`mb-4 px-3 py-2 rounded text-xs font-medium ${
              authMessage.type === "error"
                ? "bg-red-50 text-red-700 border border-red-200"
                : "bg-emerald-50 text-emerald-700 border border-emerald-200"
            }`}
          >
            {authMessage.text}
          </div>
        )}

        {/* Biểu mẫu đăng nhập */}
        <form onSubmit={handleSignIn} className="space-y-4">
          {/* Field #1: Work Email / Username */}
          <div>
            <label
              htmlFor="identifier-input"
              className="block text-xs font-semibold text-slate-700 mb-1.5"
            >
              Work Email / Username
            </label>
            <input
              id="identifier-input"
              type="text"
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              placeholder="e.g. owner@nguyenkhoa.vn or admin"
              className="w-full px-3 py-2 text-xs text-slate-800 bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #2: Password & Field #3: Forgot password? Link */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label
                htmlFor="password-input"
                className="block text-xs font-semibold text-slate-700"
              >
                Password
              </label>
              <button
                type="button"
                onClick={() => setShowForgotModal(true)}
                className="text-[11px] font-medium text-blue-600 hover:text-blue-700 hover:underline"
              >
                Forgot password?
              </button>
            </div>
            <input
              id="password-input"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full px-3 py-2 text-xs text-slate-800 bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #4: Sign In to Workspace Button */}
          <div className="pt-1">
            <button
              type="submit"
              disabled={loginMutation.isPending}
              className="w-full py-2.5 px-4 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition-colors disabled:opacity-60 flex items-center justify-center gap-2"
            >
              {loginMutation.isPending && (
                <span className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
              )}
              {loginMutation.isPending ? "Signing In..." : "Sign In to Workspace"}
            </button>
          </div>
        </form>
      </div>

      {/* Modal 1.2: Reset Personal Password (UC-04 - Section III.1.2) */}
      <ResetPasswordModal
        isOpen={showForgotModal}
        onClose={() => setShowForgotModal(false)}
      />
    </div>
  );
}
