import { useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { authApi } from '../api/authApi';
import { getErrorMessage } from '../store/SessionContext';
import type { ResetPasswordRequest } from '../types';

interface FeedbackMessage {
  type: 'error' | 'success';
  text: string;
}

export default function ResetPasswordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const emailFromUrl = searchParams.get('email') || '';
  const tokenFromUrl = searchParams.get('token') || '';

  const [email, setEmail] = useState(emailFromUrl);
  const [tokenOrOtp, setTokenOrOtp] = useState(tokenFromUrl);
  const [newPassword, setNewPassword] = useState('');
  const [confirmNewPassword, setConfirmNewPassword] = useState('');
  const [feedback, setFeedback] = useState<FeedbackMessage | null>(null);
  const [isResetComplete, setIsResetComplete] = useState(false);

  const resetPasswordMutation = useMutation({
    mutationFn: (payload: ResetPasswordRequest) => authApi.resetPassword(payload),
    onSuccess: (res) => {
      setIsResetComplete(true);
      setFeedback({
        type: 'success',
        text:
          res?.message ||
          'Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới.'
      });
    },
    onError: (err: unknown) => {
      setFeedback({
        type: 'error',
        text: getErrorMessage(err)
      });
    }
  });

  const handleResetSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();

    if (!email.trim() || !tokenOrOtp.trim()) {
      setFeedback({
        type: 'error',
        text: 'Thiếu email khôi phục hoặc mã đặt lại. Vui lòng dùng liên kết trong hộp thư của bạn.'
      });
      return;
    }

    if (!newPassword.trim() || !confirmNewPassword.trim()) {
      setFeedback({
        type: 'error',
        text: 'Vui lòng nhập Mật khẩu mới và Xác nhận mật khẩu.'
      });
      return;
    }

    if (newPassword !== confirmNewPassword) {
      setFeedback({
        type: 'error',
        text: 'Mật khẩu mới và mật khẩu xác nhận không khớp.'
      });
      return;
    }

    setFeedback(null);
    resetPasswordMutation.mutate({
      email: email.trim(),
      tokenOrOtp: tokenOrOtp.trim(),
      newPassword,
      confirmNewPassword
    });
  };

  return (
    <div className="min-h-screen w-full bg-[#F8FAFC] flex items-center justify-center p-4 relative select-none">
      <div className="w-full max-w-[420px] bg-white rounded-2xl shadow-lg px-8 py-9 border border-slate-200">
        <div className="text-center mb-6">
          <div className="w-12 h-12 rounded-xl bg-blue-600 text-white font-extrabold text-xl flex items-center justify-center mx-auto mb-3 shadow-xs">
            W
          </div>
          <h1 className="text-xl font-extrabold tracking-tight text-slate-900">
            Hệ thống Quản lý Sản xuất WMMS
          </h1>
          <p className="text-sm text-slate-500 mt-1">Xưởng gỗ Nguyên Khoa Hà Nam</p>
          <p className="text-sm font-semibold text-blue-600 mt-2">Đặt mật khẩu mới cho tài khoản</p>
        </div>

        {feedback && (
          <div
            className={`mb-5 px-4 py-3 rounded-lg text-sm font-medium whitespace-pre-line ${
              feedback.type === 'error'
                ? 'bg-rose-50 text-rose-700 border border-rose-200'
                : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
            }`}
          >
            {feedback.text}
          </div>
        )}

        {isResetComplete ? (
          <div className="space-y-4">
            <button
              type="button"
              onClick={() => navigate('/login')}
              className="w-full py-2.5 px-4 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition-colors cursor-pointer"
            >
              Quay lại đăng nhập
            </button>
          </div>
        ) : (
          <form onSubmit={handleResetSubmit} className="space-y-4">
            <div>
              <label
                htmlFor="reset-email-input"
                className="block text-sm font-medium text-slate-700 mb-1.5"
              >
                Email tài khoản
              </label>
              <input
                id="reset-email-input"
                type="email"
                value={email}
                readOnly={Boolean(emailFromUrl)}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="nhanvien@nguyenkhoa.vn"
                className={`w-full px-3.5 py-2.5 text-sm text-slate-800 border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600 ${
                  emailFromUrl ? 'bg-slate-100 text-slate-500 cursor-not-allowed' : 'bg-white'
                }`}
              />
            </div>

            {!tokenFromUrl && (
              <div>
                <label
                  htmlFor="reset-token-input"
                  className="block text-sm font-medium text-slate-700 mb-1.5"
                >
                  Mã khôi phục / OTP 6 chữ số
                </label>
                <input
                  id="reset-token-input"
                  type="text"
                  value={tokenOrOtp}
                  onChange={(e) => setTokenOrOtp(e.target.value)}
                  placeholder="Nhập OTP hoặc mã khôi phục"
                  className="w-full px-3.5 py-2.5 text-sm text-slate-800 bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
                />
              </div>
            )}

            <div>
              <label
                htmlFor="new-password-input"
                className="block text-sm font-medium text-slate-700 mb-1.5"
              >
                Mật khẩu mới
              </label>
              <input
                id="new-password-input"
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full px-3.5 py-2.5 text-sm text-slate-800 bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>

            <div>
              <label
                htmlFor="confirm-password-input"
                className="block text-sm font-medium text-slate-700 mb-1.5"
              >
                Xác nhận mật khẩu
              </label>
              <input
                id="confirm-password-input"
                type="password"
                value={confirmNewPassword}
                onChange={(e) => setConfirmNewPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full px-3.5 py-2.5 text-sm text-slate-800 bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>

            <div className="pt-2">
              <button
                type="submit"
                disabled={resetPasswordMutation.isPending}
                className="w-full py-2.5 px-4 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition-colors disabled:opacity-60 inline-flex items-center justify-center gap-2 cursor-pointer"
              >
                {resetPasswordMutation.isPending && (
                  <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                )}
                {resetPasswordMutation.isPending ? 'Đang cập nhật mật khẩu...' : 'Xác nhận mật khẩu mới'}
              </button>
            </div>

            <div className="text-center pt-1">
              <button
                type="button"
                onClick={() => navigate('/login')}
                className="text-xs font-semibold text-blue-600 hover:text-blue-700 hover:underline cursor-pointer"
              >
                ← Quay lại đăng nhập
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
