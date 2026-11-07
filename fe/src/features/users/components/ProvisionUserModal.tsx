import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { adminApi } from "../api/userApi";

export interface ProvisionUserModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
  onError: (message: string) => void;
}

function mapSelectedOptionToRole(optionText = "") {
  if (optionText.includes("kinh doanh")) {
    return {
      code: "SALES",
      label: "NHÂN VIÊN KINH DOANH",
    };
  }
  if (optionText.includes("Quản đốc")) {
    return {
      code: "LEADER",
      label: "QUẢN ĐỐC SẢN XUẤT",
    };
  }
  if (optionText.includes("QC")) {
    return {
      code: "QC",
      label: "NHÂN VIÊN QC",
    };
  }
  if (optionText.includes("Thủ kho")) {
    return {
      code: "WAREHOUSE",
      label: "THỦ KHO",
    };
  }
  if (optionText.includes("mua hàng")) {
    return {
      code: "PURCHASING",
      label: "NHÂN VIÊN MUA HÀNG",
    };
  }
  return {
    code: "OWNER",
    label: "CHỦ XƯỞNG",
  };
}

/**
 * Modal 3.2: Provision New User Account (MOD-ADM-01 - Section III.3.2)
 * Gọi REST API: POST /api/v1/users (UC-08)
 */
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
  const [assignedRoleOption, setAssignedRoleOption] = useState(
    "Chủ xưởng (Điều hành & Kỹ thuật)",
  );
  const [initialPasswordInput, setInitialPasswordInput] = useState("");
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
          "Tạo tài khoản nhân viên mới thành công! Thông tin kích hoạt và mật khẩu tạm đã được gửi.",
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
        "Vui lòng nhập Họ tên nhân viên, Tên đăng nhập và Email công ty.",
      );
      return;
    }

    const emailLower = email.toLowerCase();
    const roleMapping = mapSelectedOptionToRole(assignedRoleOption);

    createMutation.mutate({
      username,
      email: emailLower,
      fullName,
      phone: phone || undefined,
      initialPassword: initialPasswordInput || "Welcome@2026",
      roleCodes: [roleMapping.code],
    });
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[500px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden">
        {/* Header Modal */}
        <div className="px-6 pt-5 pb-3 flex items-center justify-between border-b border-slate-100">
          <h2 className="text-sm font-bold text-slate-900">
            Cấp tài khoản người dùng mới
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1"
          >
            ×
          </button>
        </div>

        {/* Thông báo lỗi cục bộ nếu có */}
        {localError && (
          <div className="mx-6 mt-4 p-3 rounded-lg text-xs bg-rose-50 border border-rose-200 text-rose-700">
            {localError}
          </div>
        )}

        <form onSubmit={handleCreateAccount} className="px-6 py-4 space-y-3.5">
          {/* Field #1: Full Employee Name */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Họ tên nhân viên
            </label>
            <input
              type="text"
              required
              value={fullNameInput}
              onChange={(e) => setFullNameInput(e.target.value)}
              placeholder="VD: Trần Văn Bình"
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #2: Username */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Tên đăng nhập
            </label>
            <input
              type="text"
              required
              value={usernameInput}
              onChange={(e) => setUsernameInput(e.target.value)}
              placeholder="VD: binh.tran"
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #3: Company Email */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Email công ty
            </label>
            <input
              type="email"
              required
              value={companyEmailInput}
              onChange={(e) => setCompanyEmailInput(e.target.value)}
              placeholder="binh.tran@nguyenkhoa.vn"
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #4: Phone Number (Optional) */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Số điện thoại{" "}
              <span className="text-slate-400 font-normal">(Không bắt buộc)</span>
            </label>
            <input
              type="tel"
              value={phoneInput}
              onChange={(e) => setPhoneInput(e.target.value)}
              placeholder="e.g. 0901234567"
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #5: Assigned Role */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Vai trò được gán
            </label>
            <select
              value={assignedRoleOption}
              onChange={(e) => setAssignedRoleOption(e.target.value)}
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option>
                Chủ xưởng (Điều hành &amp; Kỹ thuật)
              </option>
              <option>Nhân viên kinh doanh (Nhập PO Excel và khởi tạo Space)</option>
              <option>Quản đốc sản xuất (Điều phối Kanban xưởng)</option>
              <option>Nhân viên QC (Cổng chất lượng tại trạm)</option>
              <option>Thủ kho (Nhập và xuất kho)</option>
              <option>Nhân viên mua hàng (Tìm NCC và lập PO)</option>
            </select>
          </div>

          {/* Field #6: Initial Password */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Mật khẩu ban đầu
            </label>
            <input
              type="text"
              value={initialPasswordInput}
              onChange={(e) => setInitialPasswordInput(e.target.value)}
              className="w-full px-3 py-2 text-xs bg-slate-50 text-slate-800 border border-slate-300 rounded-md focus:outline-none"
            />
          </div>

          {/* Footer Buttons: Cancel & Create Account */}
          <div className="pt-2 flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              disabled={createMutation.isPending}
              className="px-3.5 py-2 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50 transition"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={createMutation.isPending}
              className="px-4 py-2 text-xs font-semibold text-white bg-[#2563eb] hover:bg-blue-700 rounded-md shadow-sm transition disabled:opacity-60 flex items-center gap-1.5"
            >
              {createMutation.isPending && (
                <span className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
              )}
              {createMutation.isPending ? "Đang tạo..." : "Tạo tài khoản"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
