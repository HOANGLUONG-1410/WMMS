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

const ALL_ROLES = [
  {
    code: "SALES",
    title: "Nhân viên kinh doanh",
    desc: "Tiếp nhận PO, làm sạch dữ liệu & tạo đơn hàng",
    badgeClass: "bg-blue-50 text-blue-700 border-blue-200",
  },
  {
    code: "PLANNER",
    title: "Kế hoạch vật tư",
    desc: "Bóc tách định mức BOM & gom yêu cầu mua hàng (PR)",
    badgeClass: "bg-indigo-50 text-indigo-700 border-indigo-200",
  },
  {
    code: "OWNER",
    title: "Chủ xưởng & Kỹ thuật",
    desc: "Duyệt đơn hàng, phê duyệt BOM & lệnh mua vật tư",
    badgeClass: "bg-amber-50 text-amber-800 border-amber-200",
  },
  {
    code: "LEADER",
    title: "Quản đốc sản xuất",
    desc: "Điều phối Kanban xưởng, trạm máy & yêu cầu cấp phát",
    badgeClass: "bg-orange-50 text-orange-800 border-orange-200",
  },
  {
    code: "QC",
    title: "Kiểm soát chất lượng",
    desc: "Cổng kiểm định chất lượng tại trạm & xử lý lỗi",
    badgeClass: "bg-rose-50 text-rose-700 border-rose-200",
  },
  {
    code: "WAREHOUSE",
    title: "Thủ kho",
    desc: "Quản lý tồn kho 3 cấp, xác nhận nhập hàng & xuất kho",
    badgeClass: "bg-cyan-50 text-cyan-800 border-cyan-200",
  },
  {
    code: "PURCHASING",
    title: "Nhân viên mua hàng",
    desc: "Lập PO gửi nhà cung cấp & theo dõi tiến độ giao hàng",
    badgeClass: "bg-emerald-50 text-emerald-700 border-emerald-200",
  },
  {
    code: "ADMIN",
    title: "Quản trị hệ thống",
    desc: "Quản lý tài khoản, phân quyền đa vai trò & nhật ký kiểm toán",
    badgeClass: "bg-purple-50 text-purple-700 border-purple-200",
  },
];

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
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [status, setStatus] = useState<"ACTIVE" | "BANNED">("ACTIVE");
  const [statusReason, setStatusReason] = useState("");
  const [updateReason, setUpdateReason] = useState("");
  const [localError, setLocalError] = useState<string | null>(null);

  useEffect(() => {
    if (user) {
      setFullName(user.fullName || "");
      setEmail(user.email || "");
      setPhone(user.phone || "");
      setSelectedRoles(user.roles && user.roles.length > 0 ? [...user.roles] : ["SALES"]);
      setStatus(user.status === "ACTIVE" ? "ACTIVE" : "BANNED");
      setStatusReason("");
      setUpdateReason("");
      setLocalError(null);
    }
  }, [user]);

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!user) return;

      const trimmedName = fullName.trim();
      const trimmedEmail = email.trim().toLowerCase();
      const trimmedPhone = phone.trim();

      if (!trimmedName || !trimmedEmail) {
        throw new Error("Vui lòng nhập đầy đủ Họ và tên và Địa chỉ email.");
      }

      if (selectedRoles.length === 0) {
        throw new Error("Vui lòng gán ít nhất 1 vai trò cho nhân sự.");
      }

      // 1. Cập nhật thông tin hồ sơ và danh sách vai trò
      const res = await adminApi.updateUser(user.userId, {
        fullName: trimmedName,
        email: trimmedEmail,
        phone: trimmedPhone || undefined,
        roleCodes: selectedRoles,
        reason: updateReason.trim() || "Cập nhật hồ sơ nhân sự và vai trò đảm nhiệm",
      });

      // 2. Nếu trạng thái thay đổi, cập nhật trạng thái
      const initialStatus = user.status === "ACTIVE" ? "ACTIVE" : "BANNED";
      if (status !== initialStatus) {
        const defaultReason =
          status === "BANNED"
            ? "Tạm khóa quyền truy cập hệ thống của nhân viên"
            : "Kích hoạt lại tài khoản truy cập hệ thống của nhân viên";
        const reasonToSend = statusReason.trim() || defaultReason;
        await adminApi.updateUserStatus(user.userId, status, reasonToSend);
      }

      return res;
    },
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
      onSuccess(res?.message || "Cập nhật hồ sơ và phân quyền nhân viên thành công!");
      onClose();
    },
    onError: (err: unknown) => {
      const msg =
        (err as { message?: string })?.message ||
        "Cập nhật hồ sơ nhân viên thất bại.";
      setLocalError(msg);
      onError(msg);
    },
  });

  if (!user) return null;

  const toggleRole = (roleCode: string) => {
    setSelectedRoles((prev) => {
      if (prev.includes(roleCode)) {
        if (prev.length === 1) {
          setLocalError("Nhân sự phải có ít nhất 1 vai trò hoạt động.");
          return prev;
        }
        setLocalError(null);
        return prev.filter((r) => r !== roleCode);
      }
      setLocalError(null);
      return [...prev, roleCode];
    });
  };

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setLocalError(null);
    updateMutation.mutate();
  };

  const getInitials = (name?: string, fallback = "NV") => {
    if (!name || !name.trim()) return fallback.slice(0, 2).toUpperCase();
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  };

  const initialStatus = user.status === "ACTIVE" ? "ACTIVE" : "BANNED";
  const hasStatusChanged = status !== initialStatus;

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-[1px] flex items-center justify-center p-4 overflow-y-auto">
      <div className="w-full max-w-[620px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden my-6">
        {/* Modal Header */}
        <div className="px-6 py-4 flex items-center justify-between border-b border-slate-200 bg-[#F8FAFC]">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-blue-100 border border-blue-200 text-blue-700 font-bold text-sm flex items-center justify-center shrink-0">
              {getInitials(user.fullName, user.username)}
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-base font-bold text-slate-900">
                  Chỉnh sửa hồ sơ &amp; phân quyền
                </h2>
                <span className="px-2 py-0.5 rounded text-[11px] font-mono font-semibold bg-slate-100 text-slate-600 border border-slate-200">
                  ID: #{String(user.userId).padStart(3, "0")}
                </span>
              </div>
              <p className="text-xs text-slate-500 mt-0.5">
                Cập nhật thông tin định danh, số điện thoại, vai trò đảm nhiệm và trạng thái hoạt động.
              </p>
            </div>
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
          <div className="mx-6 mt-4 p-3 rounded-lg text-sm bg-rose-50 border border-rose-200 text-rose-700 flex items-center justify-between">
            <span>{localError}</span>
            <button
              type="button"
              onClick={() => setLocalError(null)}
              className="text-rose-500 hover:text-rose-700 text-sm font-bold ml-2"
            >
              ×
            </button>
          </div>
        )}

        <form onSubmit={handleSubmit} className="px-6 py-5 space-y-5 max-h-[calc(85vh-140px)] overflow-y-auto">
          {/* Thông tin đăng nhập & Mã tài khoản */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Tên đăng nhập (Username)
              </label>
              <div className="relative">
                <input
                  type="text"
                  disabled
                  value={user.username}
                  className="w-full px-3.5 py-2 text-sm bg-slate-50 border border-slate-300 rounded-lg text-slate-700 font-mono cursor-not-allowed select-none"
                />
                <span className="absolute right-3 top-2.5 text-xs text-slate-400">
                  🔒 Cố định
                </span>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Trạng thái hoạt động
              </label>
              <select
                value={status}
                onChange={(e) => setStatus(e.target.value as "ACTIVE" | "BANNED")}
                className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
              >
                <option value="ACTIVE">Đang hoạt động (ACTIVE)</option>
                <option value="BANNED">Tạm khóa truy cập (BANNED)</option>
              </select>
            </div>
          </div>

          {/* Lý do thay đổi trạng thái (nếu trạng thái bị đổi) */}
          {hasStatusChanged && (
            <div className="p-3 bg-amber-50/80 border border-amber-200 rounded-lg space-y-1.5 animate-in fade-in duration-150">
              <label className="block text-xs font-bold uppercase tracking-wider text-amber-800">
                Lý do thay đổi trạng thái tài khoản <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                value={statusReason}
                onChange={(e) => setStatusReason(e.target.value)}
                placeholder={
                  status === "BANNED"
                    ? "Nhập lý do tạm khóa (VD: Nhân sự tạm nghỉ phép, vi phạm quy định...)"
                    : "Nhập lý do mở khóa (VD: Kích hoạt lại sau kỳ nghỉ...)"
                }
                className="w-full px-3 py-1.5 text-sm bg-white border border-amber-300 rounded-md focus:outline-none focus:ring-2 focus:ring-amber-500 text-slate-800"
              />
            </div>
          )}

          {/* Họ và tên & Thông tin liên hệ */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Họ và tên nhân viên <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                required
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                placeholder="VD: Nguyễn Văn Hùng"
                className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Số điện thoại liên hệ
              </label>
              <input
                type="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="VD: 0908123456"
                className="w-full px-3.5 py-2 text-sm font-mono bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1.5">
              Địa chỉ email công ty <span className="text-rose-500">*</span>
            </label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="nhanvien@nguyenkhoa.vn"
              className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
            />
          </div>

          {/* Phân quyền Vai trò đảm nhiệm (Multi-Role RBAC) */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <label className="block text-sm font-medium text-slate-700">
                Vai trò đảm nhiệm <span className="text-rose-500">*</span>
              </label>
              <span className="text-xs text-slate-500">
                Đã chọn: <strong className="text-blue-600">{selectedRoles.length}</strong> vai trò
              </span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 max-h-[220px] overflow-y-auto p-1 border border-slate-200 rounded-lg bg-slate-50/50">
              {ALL_ROLES.map((role) => {
                const isSelected = selectedRoles.includes(role.code);
                return (
                  <button
                    key={role.code}
                    type="button"
                    onClick={() => toggleRole(role.code)}
                    className={`p-3 rounded-lg border text-left transition flex items-start gap-2.5 cursor-pointer ${
                      isSelected
                        ? "bg-white border-blue-600 shadow-xs ring-1 ring-blue-600/30"
                        : "bg-white border-slate-200 hover:border-slate-300 hover:bg-slate-50/80"
                    }`}
                  >
                    <input
                      type="checkbox"
                      checked={isSelected}
                      onChange={() => {}} // Controlled by button onClick
                      className="mt-0.5 rounded border-slate-300 text-blue-600 focus:ring-blue-500 cursor-pointer pointer-events-none"
                    />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <span className="text-xs font-bold text-slate-900">
                          {role.title}
                        </span>
                        <span
                          className={`px-1.5 py-0.2 text-[10px] font-bold uppercase rounded border ${role.badgeClass}`}
                        >
                          {role.code}
                        </span>
                      </div>
                      <p className="text-[11px] text-slate-500 mt-0.5 leading-snug line-clamp-2">
                        {role.desc}
                      </p>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Lý do cập nhật hồ sơ (Ghi chú kiểm toán) */}
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1.5">
              Ghi chú lý do cập nhật{" "}
              <span className="text-slate-400 font-normal">(Lưu vào nhật ký kiểm toán)</span>
            </label>
            <input
              type="text"
              value={updateReason}
              onChange={(e) => setUpdateReason(e.target.value)}
              placeholder="VD: Bổ sung vai trò Quản đốc, điều chuyển phòng ban..."
              className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
            />
          </div>

          {/* Modal Footer */}
          <div className="pt-4 border-t border-slate-200 flex items-center justify-between gap-3">
            <span className="text-xs text-slate-400">
              Lần đăng nhập cuối:{" "}
              <span className="text-slate-600 font-medium">
                {user.lastLoginAt
                  ? new Date(user.lastLoginAt).toLocaleString("vi-VN")
                  : "Chưa từng đăng nhập"}
              </span>
            </span>

            <div className="flex items-center gap-2.5">
              <button
                type="button"
                onClick={onClose}
                disabled={updateMutation.isPending}
                className="px-4 py-2 text-sm font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition cursor-pointer"
              >
                Hủy bỏ
              </button>
              <button
                type="submit"
                disabled={updateMutation.isPending}
                className="px-5 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-xs transition disabled:opacity-60 flex items-center gap-2 cursor-pointer"
              >
                {updateMutation.isPending && (
                  <span className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                )}
                <span>{updateMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}</span>
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}


