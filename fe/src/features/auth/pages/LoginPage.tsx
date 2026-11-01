import { useState, type FormEvent } from "react";
import { getErrorMessage } from "../store/SessionContext";
import { useLogin } from "../hooks/useLogin";
import ResetPasswordModal from "../components/ResetPasswordModal";

interface FeedbackMessage {
  type: "error" | "success";
  text: string;
}

export default function LoginPage() {
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [authMessage, setAuthMessage] = useState<FeedbackMessage | null>(null);
  const [showForgotModal, setShowForgotModal] = useState(false);

  const loginMutation = useLogin();

  const handleSignIn = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!identifier.trim() || !password.trim()) {
      setAuthMessage({
        type: "error",
        text: "Please enter both Work Email / Username and Password.",
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
      <div className="w-full max-w-[380px] bg-white rounded-xl shadow-2xl px-7 py-8 border border-slate-100">
        <div className="text-center mb-6">
          <h1 className="text-[17px] font-extrabold tracking-tight text-slate-900 uppercase">
            WMMS PRODUCTION SUITE
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            Nguyen Khoa Ha Nam Wood Factory
          </p>
        </div>

        {/* Thông báo lỗi hiển thị inline ngay trên 2 ô input khi đăng nhập thất bại */}
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

        <form onSubmit={handleSignIn} className="space-y-4">
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

      <ResetPasswordModal
        isOpen={showForgotModal}
        onClose={() => setShowForgotModal(false)}
      />
    </div>
  );
}
