import { useState, useEffect, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { adminApi } from "../api/userApi";
import type { UserDto } from "../types";

export interface EditUserModalProps {
  user: UserDto | null;
  onClose: () => void;
  onSuccess: (message: string) => void;
  onError: (message: string) => void;
}

/**
 * Modal Edit User (UC-09 & UC-10)
 * Hỗ trợ chỉnh sửa thông tin hồ sơ và khóa/mở khóa tài khoản nhân sự
 */
export default function EditUserModal({
  user,
  onClose,
  onSuccess,
  onError,
}: EditUserModalProps) {
  const queryClient = useQueryClient();
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");

  useEffect(() => {
    if (user) {
      setFullName(user.fullName || "");
      setEmail(user.email || "");
      setPhone(user.phone || "");
    }
  }, [user]);

  // Mutation cập nhật thông tin (UC-09: PUT /users/{id})
  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!user) return;
      return await adminApi.updateUser(user.userId, {
        fullName,
        email,
        phone,
      });
    },
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
      onSuccess(res?.message || "Cập nhật hồ sơ người dùng thành công!");
      onClose();
    },
    onError: (err: unknown) => {
      onError((err as { message?: string })?.message || "Cập nhật hồ sơ người dùng thất bại.");
    },
  });

  // Mutation khóa / mở khóa (UC-10: PATCH /users/{id}/status)
  const toggleStatusMutation = useMutation({
    mutationFn: async () => {
      if (!user) return;
      const isCurrentlyActive = user.status === "ACTIVE";
      const apiStatus = isCurrentlyActive ? "BANNED" : "ACTIVE";
      const reason = `Quản trị viên đổi trạng thái tài khoản`;
      return await adminApi.updateUserStatus(user.userId, apiStatus, reason);
    },
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
      onSuccess(res?.message || "Cập nhật trạng thái tài khoản thành công!");
      onClose();
    },
    onError: (err: unknown) => {
      onError((err as { message?: string })?.message || "Đổi trạng thái tài khoản thất bại.");
    },
  });

  if (!user) return null;

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    updateMutation.mutate();
  };

  const isActive = user.status === "ACTIVE";

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[460px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden">
        <div className="px-6 pt-5 pb-3 flex items-center justify-between border-b border-slate-100">
          <h2 className="text-sm font-bold text-slate-900">
            Sửa tài khoản &amp; vai trò nhân viên
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1"
          >
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-3.5">
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Tên nhân viên
            </label>
            <input
              type="text"
              required
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Địa chỉ email
            </label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Số điện thoại
            </label>
            <input
              type="text"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              placeholder="e.g. 0901234567"
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div className="pt-2 flex items-center justify-between">
            <button
              type="button"
              disabled={toggleStatusMutation.isPending}
              onClick={() => toggleStatusMutation.mutate()}
              className={`px-3 py-1.5 text-xs font-semibold rounded-md transition ${
                isActive
                  ? "text-rose-700 bg-rose-50 border border-rose-200 hover:bg-rose-100"
                  : "text-emerald-700 bg-emerald-50 border border-emerald-200 hover:bg-emerald-100"
              }`}
            >
              {toggleStatusMutation.isPending
                ? "Đang cập nhật..."
                : isActive
                ? "Tạm khóa / Cấm tài khoản"
                : "Bỏ cấm tài khoản"}
            </button>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={onClose}
                className="px-3.5 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50 transition"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={updateMutation.isPending}
                className="px-4 py-1.5 text-xs font-semibold text-white bg-[#2563eb] hover:bg-blue-700 rounded-md shadow-sm transition disabled:opacity-60"
              >
                {updateMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
