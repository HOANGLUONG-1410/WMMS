import { useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { authApi } from '../api/authApi';
import type { ResetPasswordRequest } from '../types';

interface FeedbackMessage {
  type: 'error' | 'success';
  text: string;
}

function getErrorMessage(error: unknown): string {
  if (error && typeof error === 'object') {
    // 1. Direct errors array (e.g. from ApiError)
    if ('errors' in error && Array.isArray((error as { errors?: unknown }).errors)) {
      const errs = (error as { errors: unknown[] }).errors.map(String).filter(Boolean);
      if (errs.length > 0) return errs.join('\n');
    }

    // 2. Axios response payload (e.g. error.response.data.errors)
    if ('response' in error && typeof (error as { response?: unknown }).response === 'object') {
      const resData = (error as { response?: { data?: unknown } }).response?.data;
      if (resData && typeof resData === 'object') {
        if ('errors' in resData && Array.isArray((resData as { errors?: unknown }).errors)) {
          const errs = (resData as { errors: unknown[] }).errors.map(String).filter(Boolean);
          if (errs.length > 0) return errs.join('\n');
        }
        if ('message' in resData && typeof (resData as { message?: unknown }).message === 'string') {
          return (resData as { message: string }).message;
        }
      }
    }

    // 3. Fallback to top-level message
    if ('message' in error && typeof (error as { message?: unknown }).message === 'string') {
      return (error as { message: string }).message;
    }
  }
  return 'Invalid or expired password reset token. Please request a new recovery link (MSG06).';
}

/**
 * Màn hình UC-05: Confirm Password Reset
 * Hỗ trợ đường dẫn khôi phục từ email:
 * Ví dụ: http://localhost:5173/reset-password?email=tranhunganhc%40gmail.com&token=mpuiJ_fn97y...
 */
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
          'Này là fail rồi'
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
        text: 'Missing recovery email or reset token. Please use the link from your email inbox (MSG06).'
      });
      return;
    }

    if (!newPassword.trim() || !confirmNewPassword.trim()) {
      setFeedback({
        type: 'error',
        text: 'Please enter both New Password and Confirm Password (MSG01).'
      });
      return;
    }

    if (newPassword !== confirmNewPassword) {
      setFeedback({
        type: 'error',
        text: 'New password and confirmation password do not match (MSG13).'
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
    <div className="min-h-screen w-full bg-[#0d1528] flex items-center justify-center p-4 relative select-none">
      {/* Thẻ Reset Password chính giữa đồng bộ thiết kế với LoginPage */}
      <div className="w-full max-w-[380px] bg-white rounded-xl shadow-2xl px-7 py-8 border border-slate-100">
        {/* Tiêu đề hệ thống */}
        <div className="text-center mb-6">
          <h1 className="text-[17px] font-extrabold tracking-tight text-slate-900 uppercase">
            WMMS PRODUCTION SUITE
          </h1>
          <p className="text-xs text-slate-500 mt-1">Nguyen Khoa Ha Nam Wood Factory</p>
          <p className="text-xs font-semibold text-blue-600 mt-2">Set New Account Password</p>
        </div>

        {/* Thông báo phản hồi nếu có */}
        {feedback && (
          <div
            className={`mb-4 px-3 py-2 rounded text-xs font-medium whitespace-pre-line ${
              feedback.type === 'error'
                ? 'bg-red-50 text-red-700 border border-red-200'
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
              className="w-full py-2.5 px-4 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition-colors"
            >
              Back to Sign In
            </button>
          </div>
        ) : (
          <form onSubmit={handleResetSubmit} className="space-y-4">
            {/* Hiển thị Email nhận từ URL hoặc cho nhập nếu mở trực tiếp */}
            <div>
              <label
                htmlFor="reset-email-input"
                className="block text-xs font-semibold text-slate-700 mb-1.5"
              >
                Account Email
              </label>
              <input
                id="reset-email-input"
                type="email"
                value={email}
                readOnly={Boolean(emailFromUrl)}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="employee@nguyenkhoa.vn"
                className={`w-full px-3 py-2 text-xs text-slate-800 border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                  emailFromUrl ? 'bg-slate-100 text-slate-500 cursor-not-allowed' : 'bg-white'
                }`}
              />
            </div>

            {/* Chỉ hiển thị ô nhập OTP/Token nếu đường dẫn không có sẵn query ?token=... */}
            {!tokenFromUrl && (
              <div>
                <label
                  htmlFor="reset-token-input"
                  className="block text-xs font-semibold text-slate-700 mb-1.5"
                >
                  Recovery Token / 6-Digit OTP
                </label>
                <input
                  id="reset-token-input"
                  type="text"
                  value={tokenOrOtp}
                  onChange={(e) => setTokenOrOtp(e.target.value)}
                  placeholder="Enter OTP or recovery token"
                  className="w-full px-3 py-2 text-xs text-slate-800 bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                />
              </div>
            )}

            {/* Field #1: New Password */}
            <div>
              <label
                htmlFor="new-password-input"
                className="block text-xs font-semibold text-slate-700 mb-1.5"
              >
                New Password
              </label>
              <input
                id="new-password-input"
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full px-3 py-2 text-xs text-slate-800 bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              />
            </div>

            {/* Field #2: Confirm Password */}
            <div>
              <label
                htmlFor="confirm-password-input"
                className="block text-xs font-semibold text-slate-700 mb-1.5"
              >
                Confirm Password
              </label>
              <input
                id="confirm-password-input"
                type="password"
                value={confirmNewPassword}
                onChange={(e) => setConfirmNewPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full px-3 py-2 text-xs text-slate-800 bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              />
            </div>

            {/* Submit Button */}
            <div className="pt-1">
              <button
                type="submit"
                disabled={resetPasswordMutation.isPending}
                className="w-full py-2.5 px-4 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition-colors disabled:opacity-60 inline-flex items-center justify-center gap-2"
              >
                {resetPasswordMutation.isPending && (
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
                {resetPasswordMutation.isPending ? 'Updating Password...' : 'Confirm New Password'}
              </button>
            </div>

            {/* Back to Login link */}
            <div className="text-center pt-1">
              <button
                type="button"
                onClick={() => navigate('/login')}
                className="text-[11px] font-medium text-blue-600 hover:text-blue-700 hover:underline"
              >
                ← Back to Sign In
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

