import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useWmms } from '../context/WmmsContext';
import { authApi } from '../services/wmmsApi';

/**
 * Màn hình III.1: Authorization (1.1 Login & 1.2 Forgot Password Modal)
 * Tuân thủ 100% thiết kế giao diện và bảng Field Description trong Report 3 (Section III.1):
 * - Nền tối `#0d1528`, thẻ trắng chính giữa bo góc.
 * - Tiêu đề: `WMMS PRODUCTION SUITE` | Phụ đề: `Nguyen Khoa Ha Nam Wood Factory`.
 * - Trường #1: `Work Email / Username` (mặc định `owner@nguyenkhoa.vn`).
 * - Trường #2: `Password` + Link #3 `Forgot password?` nằm cùng hàng bên phải nhãn Password.
 * - Nút #4: `Sign In to Workspace`.
 * - Modal 1.2 (`Reset Personal Password`):
 *   + Mô tả: "Enter your registered employee email. System will generate a single-use authentication token sent to your company inbox."
 *   + Trường #1: `Employee Email Address` (`employee@nguyenkhoa.vn`).
 *   + Nút #2: `Cancel` | Nút #3: `Send Recovery Token`.
 */
export default function LoginPage() {
  const navigate = useNavigate();
  const { loginWorkspace } = useWmms();

  // State cho biểu mẫu Đăng nhập (Section III.1.1 - UC-01)
  const [identifier, setIdentifier] = useState('owner@nguyenkhoa.vn');
  const [password, setPassword] = useState('Admin@123');
  const [loading, setLoading] = useState(false);
  const [authMessage, setAuthMessage] = useState(null);

  // State cho Modal Quên mật khẩu (Section III.1.2 - UC-04)
  const [showForgotModal, setShowForgotModal] = useState(false);
  const [recoveryEmail, setRecoveryEmail] = useState('employee@nguyenkhoa.vn');
  const [recoveryFeedback, setRecoveryFeedback] = useState(null);
  const [sendingToken, setSendingToken] = useState(false);

  /**
   * UC-01: Xử lý sự kiện bấm nút "Sign In to Workspace"
   */
  const handleSignIn = async (e) => {
    e.preventDefault();
    if (!identifier.trim() || !password.trim()) {
      setAuthMessage({
        type: 'error',
        text: 'Please enter both Work Email / Username and Password (MSG01).'
      });
      return;
    }

    setLoading(true);
    setAuthMessage(null);
    const res = await loginWorkspace(identifier, password);
    setLoading(false);

    if (res.success) {
      // Điều hướng tới workspace tương ứng với vai trò (POST-2 trong UC-01)
      if (res.role === 'SALES') {
        navigate('/orders');
      } else if (res.role === 'ADMIN') {
        navigate('/admin/users');
      } else {
        navigate('/admin/users');
      }
    }
  };

  /**
   * UC-04: Xử lý sự kiện bấm nút "Send Recovery Token" trong Modal Reset Personal Password
   */
  const handleSendRecoveryToken = async (e) => {
    e.preventDefault();
    if (!recoveryEmail.trim()) {
      setRecoveryFeedback({
        type: 'error',
        text: 'Please enter your registered employee email address.'
      });
      return;
    }

    setSendingToken(true);
    setRecoveryFeedback(null);
    try {
      const apiRes = await authApi.forgotPassword(recoveryEmail.trim());
      setRecoveryFeedback({
        type: 'success',
        text:
          apiRes?.message ||
          'A 6-digit verification OTP has been sent to your registered email address. Valid for 15 minutes. (MSG-AUT-05)'
      });
    } catch {
      // Hiển thị thông báo chuẩn MSG-AUT-05 theo đặc tả UC-04 chống dò quét tài khoản
      setRecoveryFeedback({
        type: 'success',
        text: 'A 6-digit verification OTP has been sent to your registered email address. Valid for 15 minutes. (MSG-AUT-05)'
      });
    } finally {
      setSendingToken(false);
    }
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
          <p className="text-xs text-slate-500 mt-1">Nguyen Khoa Ha Nam Wood Factory</p>
        </div>

        {/* Thông báo phản hồi nếu có */}
        {authMessage && (
          <div
            className={`mb-4 px-3 py-2 rounded text-xs font-medium ${
              authMessage.type === 'error'
                ? 'bg-red-50 text-red-700 border border-red-200'
                : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
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
              htmlFor="work-email-input"
              className="block text-xs font-semibold text-slate-700 mb-1.5"
            >
              Work Email / Username
            </label>
            <input
              id="work-email-input"
              type="text"
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              placeholder="owner@nguyenkhoa.vn"
              className="w-full px-3 py-2 text-xs text-slate-800 bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
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
                onClick={() => {
                  setRecoveryFeedback(null);
                  setShowForgotModal(true);
                }}
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
              className="w-full px-3 py-2 text-xs text-slate-800 bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            />
          </div>

          {/* Field #4: Sign In to Workspace Button */}
          <div className="pt-1">
            <button
              type="submit"
              disabled={loading}
              className="w-full py-2.5 px-4 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition-colors disabled:opacity-60"
            >
              {loading ? 'Signing In...' : 'Sign In to Workspace'}
            </button>
          </div>
        </form>
      </div>

      {/* Modal 1.2: Reset Personal Password (UC-04 - Section III.1.2) */}
      {showForgotModal && (
        <div className="fixed inset-0 z-50 bg-slate-950/70 backdrop-blur-[1px] flex items-center justify-center p-4">
          <div className="w-full max-w-[490px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden animate-in fade-in zoom-in-95 duration-150">
            {/* Header Modal */}
            <div className="px-6 pt-5 pb-3 flex items-center justify-between border-b border-slate-100">
              <h2 className="text-sm font-bold text-slate-900">Reset Personal Password</h2>
              <button
                type="button"
                onClick={() => setShowForgotModal(false)}
                className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1"
                aria-label="Close modal"
              >
                ×
              </button>
            </div>

            {/* Nội dung Modal */}
            <form onSubmit={handleSendRecoveryToken} className="px-6 py-4 space-y-4">
              <p className="text-xs text-slate-500 leading-relaxed">
                Enter your registered employee email. System will generate a single-use
                authentication token sent to your company inbox.
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
                    recoveryFeedback.type === 'error'
                      ? 'bg-red-50 text-red-700 border border-red-200'
                      : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                  }`}
                >
                  {recoveryFeedback.text}
                </div>
              )}

              {/* Footer Buttons: Field #2 Cancel & Field #3 Send Recovery Token */}
              <div className="pt-2 flex items-center justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowForgotModal(false)}
                  className="px-3.5 py-2 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50 transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={sendingToken}
                  className="px-4 py-2 text-xs font-semibold text-white bg-[#2563eb] hover:bg-blue-700 rounded-md shadow-sm transition disabled:opacity-60"
                >
                  {sendingToken ? 'Sending...' : 'Send Recovery Token'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
