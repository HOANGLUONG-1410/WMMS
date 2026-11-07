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
        text: "Vui lòng nhập đầy đủ Email công việc / Tên đăng nhập và Mật khẩu.",
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
    <div className="min-h-screen w-full bg-[#F8FAFC] flex items-center justify-center p-4 relative select-none">
      <div className="w-full max-w-[420px] bg-white rounded-2xl shadow-lg px-8 py-9 border border-slate-200">
        <div className="text-center mb-7">
          <div className="w-12 h-12 rounded-xl bg-blue-600 text-white font-extrabold text-xl flex items-center justify-center mx-auto mb-3 shadow-xs">
            W
          </div>
          <h1 className="text-xl font-extrabold tracking-tight text-slate-900">
            Hệ thống Quản lý Sản xuất WMMS
          </h1>
          <p className="text-sm text-slate-500 mt-1">
            Xưởng gỗ Nguyên Khoa Hà Nam
          </p>
        </div>

        {authMessage && (
          <div
            className={`mb-5 px-4 py-3 rounded-lg text-sm font-medium ${
              authMessage.type === "error"
                ? "bg-rose-50 text-rose-700 border border-rose-200"
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
              className="block text-sm font-medium text-slate-700 mb-1.5"
            >
              Email công việc / Tên đăng nhập
            </label>
            <input
              id="identifier-input"
              type="text"
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              placeholder="VD: owner@nguyenkhoa.vn hoặc admin"
              className="w-full px-3.5 py-2.5 text-sm text-slate-800 bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
            />
          </div>

          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label
                htmlFor="password-input"
                className="block text-sm font-medium text-slate-700"
              >
                Mật khẩu
              </label>
              <button
                type="button"
                onClick={() => setShowForgotModal(true)}
                className="text-xs font-semibold text-blue-600 hover:text-blue-700 hover:underline cursor-pointer"
              >
                Quên mật khẩu?
              </button>
            </div>
            <input
              id="password-input"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full px-3.5 py-2.5 text-sm text-slate-800 bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
            />
          </div>

          <div className="pt-2">
            <button
              type="submit"
              disabled={loginMutation.isPending}
              className="w-full py-2.5 px-4 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition-colors disabled:opacity-60 flex items-center justify-center gap-2 cursor-pointer"
            >
              {loginMutation.isPending && (
                <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
              )}
              {loginMutation.isPending
                ? "Đang đăng nhập..."
                : "Đăng nhập vào hệ thống"}
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

