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
      onSuccess(res?.message || "User profile updated successfully! (MSG-ADM-03)");
      onClose();
    },
    onError: (err: unknown) => {
      onError((err as { message?: string })?.message || "Failed to update user profile.");
    },
  });

  // Mutation khóa / mở khóa (UC-10: PATCH /users/{id}/status)
  const toggleStatusMutation = useMutation({
    mutationFn: async () => {
      if (!user) return;
      const isCurrentlyActive = user.status === "ACTIVE";
      const apiStatus = isCurrentlyActive ? "BANNED" : "ACTIVE";
      const reason = `Admin toggled status via MOD-ADM-01`;
      return await adminApi.updateUserStatus(user.userId, apiStatus, reason);
    },
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
      onSuccess(res?.message || "User account status updated successfully! (UC-10)");
      onClose();
    },
    onError: (err: unknown) => {
      onError((err as { message?: string })?.message || "Failed to change account status.");
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
            Edit Employee Account &amp; Role (UC-09)
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
              Employee Name
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
              Email Address
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
              Phone Number
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
                ? "Updating..."
                : isActive
                ? "Suspend / Ban Account"
                : "Unban Account"}
            </button>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={onClose}
                className="px-3.5 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50 transition"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={updateMutation.isPending}
                className="px-4 py-1.5 text-xs font-semibold text-white bg-[#2563eb] hover:bg-blue-700 rounded-md shadow-sm transition disabled:opacity-60"
              >
                {updateMutation.isPending ? "Saving..." : "Save Changes"}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
