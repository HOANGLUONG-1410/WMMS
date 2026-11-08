import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { adminApi } from "../api/userApi";

export interface ProvisionUserModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
  onError: (message: string) => void;
}

export default function ProvisionUserModal({
  isOpen,
  onClose,
  onSuccess,
  onError,
}: ProvisionUserModalProps) {
  const queryClient = useQueryClient();
  const [fullNameInput, setFullNameInput] = useState("");
  const [usernameInput, setUsernameInput] = useState("");
  const [companyEmailInput, setCompanyEmailInput] = useState("");
  const [phoneInput, setPhoneInput] = useState("");
  const [assignedRoleCode, setAssignedRoleCode] = useState("SALES");
  const [initialPasswordInput, setInitialPasswordInput] =
    useState("Welcome@2026");
  const [localError, setLocalError] = useState<string | null>(null);

  const createMutation = useMutation({
    mutationFn: async (payload: {
      username: string;
      email: string;
      fullName: string;
      phone?: string;
      initialPassword: string;
      roleCodes: string[];
    }) => {
      return await adminApi.createUser(payload);
    },
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
      setFullNameInput("");
      setUsernameInput("");
      setCompanyEmailInput("");
      setPhoneInput("");
      setLocalError(null);
      onSuccess(
        res?.message ||
          "Tạo tài khoản nhân viên mới thành công! Thông tin kích hoạt đã được lưu.",
      );
      onClose();
    },
    onError: (err: unknown) => {
      const errMsg =
        (err as { message?: string })?.message ||
        "Không thể tạo tài khoản. Vui lòng kiểm tra lại thông tin.";
      setLocalError(errMsg);
      onError(errMsg);
    },
  });

  if (!isOpen) return null;

  const handleCreateAccount = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setLocalError(null);

    const fullName = fullNameInput.trim();
    const username = usernameInput.trim();
    const email = companyEmailInput.trim();
    const phone = phoneInput.trim();

    if (!fullName || !username || !email) {
      setLocalError(
        "Vui lòng nhập đầy đủ Họ và tên, Tên đăng nhập và Email công ty.",
      );
      return;
    }

    createMutation.mutate({
      username,
      email: email.toLowerCase(),
      fullName,
      phone: phone || undefined,
      initialPassword: initialPasswordInput.trim() || "Welcome@2026",
      roleCodes: [assignedRoleCode],
    });
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[520px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden">
        {/* Modal Header */}
        <div className="px-6 py-4 flex items-center justify-between border-b border-slate-200">
          <div>
            <h2 className="text-base font-bold text-slate-900">
              Tạo tài khoản nhân viên mới
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Cấp quyền truy cập hệ thống và mật khẩu khởi tạo cho nhân sự
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-xl font-bold leading-none px-1 cursor-pointer"
          >
            ×
          </button>
        </div>

        {localError && (
          <div className="mx-6 mt-4 p-3 rounded-lg text-sm bg-rose-50 border border-rose-200 text-rose-700">
            {localError}
          </div>
        )}

        <form onSubmit={handleCreateAccount} className="px-6 py-5 space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Họ và tên nhân viên <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                required
                value={fullNameInput}
                onChange={(e) => setFullNameInput(e.target.value)}
                placeholder="VD: Trần Văn Bình"
                className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Tên đăng nhập <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                required
                value={usernameInput}
                onChange={(e) => setUsernameInput(e.target.value)}
                placeholder="VD: binh.tran"
                className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg font-mono focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Email công ty <span className="text-rose-500">*</span>
              </label>
              <input
                type="email"
                required
                value={companyEmailInput}
                onChange={(e) => setCompanyEmailInput(e.target.value)}
                placeholder="binh.tran@nguyenkhoa.vn"
                className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Số điện thoại{" "}
                <span className="text-slate-400 font-normal">(Tùy chọn)</span>
              </label>
              <input
                type="tel"
                value={phoneInput}
                onChange={(e) => setPhoneInput(e.target.value)}
                placeholder="0901234567"
                className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg font-mono focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1.5">
              Vai trò đảm nhiệm <span className="text-rose-500">*</span>
            </label>
            <select
              value={assignedRoleCode}
              onChange={(e) => setAssignedRoleCode(e.target.value)}
              className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
            >
              <option value="OWNER">Chủ xưởng (Điều hành &amp; Kỹ thuật)</option>
              <option value="SALES">
                Nhân viên kinh doanh (Tiếp nhận PO &amp; tạo đơn hàng)
              </option>
              <option value="PLANNER">
                Kế hoạch vật tư (Bóc tách BOM &amp; tính toán nhu cầu)
              </option>
              <option value="LEADER">
                Quản đốc sản xuất (Điều phối Kanban xưởng)
              </option>
              <option value="QC">
                Nhân viên QC (Kiểm soát chất lượng tại trạm)
              </option>
              <option value="WAREHOUSE">
                Thủ kho (Quản lý nhập kho &amp; xuất vật tư)
              </option>
              <option value="PURCHASING">
                Nhân viên mua hàng (Lập PO &amp; theo dõi tiến độ giao)
              </option>
              <option value="ADMIN">Quản trị viên hệ thống (ADMIN)</option>
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1.5">
              Mật khẩu khởi tạo
            </label>
            <input
              type="text"
              value={initialPasswordInput}
              onChange={(e) => setInitialPasswordInput(e.target.value)}
              className="w-full px-3.5 py-2 text-sm bg-slate-50 text-slate-800 border border-slate-300 rounded-lg font-mono focus:outline-none focus:ring-2 focus:ring-blue-600"
            />
            <p className="text-xs text-slate-500 mt-1">
              Nhân viên sẽ được yêu cầu đổi mật khẩu trong lần đăng nhập đầu tiên.
            </p>
          </div>

          <div className="pt-3 border-t border-slate-200 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              disabled={createMutation.isPending}
              className="px-4 py-2 text-sm font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition cursor-pointer"
            >
              Hủy bỏ
            </button>
            <button
              type="submit"
              disabled={createMutation.isPending}
              className="px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-xs transition disabled:opacity-60 flex items-center gap-1.5 cursor-pointer"
            >
              {createMutation.isPending && (
                <span className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
              )}
              {createMutation.isPending ? "Đang tạo..." : "Xác nhận tạo tài khoản"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

