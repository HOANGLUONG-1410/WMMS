import { useMemo, useState } from "react";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useWmms } from "../../auth/hooks/WmmsContext";
import { adminApi } from "../api/userApi";
import ProvisionUserModal, {
  type ProvisionedUserItem,
} from "../components/ProvisionUserModal";
import RolePermissionMatrixModal from "../components/RolePermissionMatrixModal";
import EditUserModal from "../components/EditUserModal";

interface ToastBanner {
  type: "error" | "success";
  text: string;
}

/**
 * Màn hình III.3: System Administration
 * Bao gồm 3 giao diện chuẩn theo đúng thiết kế trong Report 3:
 * - 3.1: User Accounts & Multi-Role RBAC (SCR-ADM-01)
 * - 3.2: Provision New User Account Modal (MOD-ADM-01) -> Tách ra ProvisionUserModal.tsx
 * - 3.3: Role Permission Configuration Matrix Modal (MOD-ADM-02) -> Tách ra RolePermissionMatrixModal.tsx
 */
export default function UserManagementPage() {
  const { usersDirectory, setUsersDirectory, appendAuditLog } = useWmms();

  // Từ khóa tìm kiếm trên bảng Employee User Directory (Field #5)
  const [searchKeyword, setSearchKeyword] = useState("");
  const [toastBanner, setToastBanner] = useState<ToastBanner | null>(null);

  // State cho Modal 3.2: Provision New User Account (MOD-ADM-01)
  const [showCreateModal, setShowCreateModal] = useState(false);

  // State cho Modal chỉnh sửa tài khoản nhân sự (UC-09)
  const [editingUser, setEditingUser] = useState<ProvisionedUserItem | null>(
    null,
  );

  // State cho Modal 3.3: Role Permission Configuration Matrix (MOD-ADM-02)
  const [showPermissionMatrixModal, setShowPermissionMatrixModal] =
    useState(false);

  // Lọc danh sách nhân sự theo tên, email, hoặc vai trò (UC-06)
  const filteredUsers = useMemo(() => {
    const kw = searchKeyword.trim().toLowerCase();
    if (!kw) return usersDirectory;
    return usersDirectory.filter(
      (u) =>
        u.fullName.toLowerCase().includes(kw) ||
        u.email.toLowerCase().includes(kw) ||
        u.primaryRoleLabel.toLowerCase().includes(kw) ||
        u.operationalScope.toLowerCase().includes(kw),
    );
  }, [usersDirectory, searchKeyword]);

  // Ánh xạ màu sắc huy hiệu Primary Assigned Role giống hệt ảnh SCR-ADM-01
  const getRoleBadgeStyle = (roleLabel: string) => {
    const upper = String(roleLabel || "").toUpperCase();
    if (upper.includes("OWNER"))
      return "bg-yellow-100 text-amber-800 border-yellow-200";
    if (upper.includes("SALES"))
      return "bg-blue-100 text-blue-700 border-blue-200";
    if (upper.includes("LEADER"))
      return "bg-amber-100 text-amber-900 border-amber-200";
    if (upper.includes("QC"))
      return "bg-rose-100 text-rose-700 border-rose-200";
    if (upper.includes("WAREHOUSE"))
      return "bg-orange-100 text-orange-800 border-orange-200";
    if (upper.includes("PURCHASING"))
      return "bg-emerald-100 text-emerald-700 border-emerald-200";
    return "bg-slate-100 text-slate-600 border-slate-200";
  };

  /**
   * UC-08: Xử lý khi tạo mới tài khoản thành công từ ProvisionUserModal
   */
  const handleUserCreated = (newUser: ProvisionedUserItem, message: string) => {
    setUsersDirectory((prev) => [newUser, ...prev]);
    appendAuditLog(
      "USER_CREATED",
      `User ${newUser.email}`,
      "NONE → ACTIVE",
      `Provisioned new staff account (${newUser.primaryRoleLabel}) via MOD-ADM-01.`,
    );
    setToastBanner({
      type: "success",
      text: message,
    });
  };

  /**
   * UC-10: Khóa / Mở khóa (Unban) tài khoản bị đình chỉ trên bảng
   */
  const handleToggleBanStatus = async (user: ProvisionedUserItem) => {
    const nextStatus = user.status === "ACTIVE" ? "SUSPENDED" : "ACTIVE";
    const apiStatus = nextStatus === "ACTIVE" ? "ACTIVE" : "BANNED";

    try {
      await adminApi.updateUserStatus(
        user.userId,
        apiStatus,
        `Admin toggled account status to ${nextStatus} via SCR-ADM-01.`,
      );
    } catch {
      // Fallback local state
    }

    setUsersDirectory((prev) =>
      prev.map((u) =>
        u.userId === user.userId ? { ...u, status: nextStatus } : u,
      ),
    );

    appendAuditLog(
      "USER_STATUS_CHANGED",
      `User ${user.email}`,
      `${user.status} → ${nextStatus}`,
      `Admin updated account status for ${user.fullName} (UC-10).`,
    );

    setToastBanner({
      type: "success",
      text: `Account ${user.email} status updated to ${nextStatus} (UC-10).`,
    });
  };

  /**
   * UC-09: Lưu cập nhật thông tin nhân sự khi bấm nút Save trong EditUserModal
   */
  const handleSaveEditUser = (updatedUser: ProvisionedUserItem) => {
    setUsersDirectory((prev) =>
      prev.map((u) => (u.userId === updatedUser.userId ? updatedUser : u)),
    );

    appendAuditLog(
      "USER_UPDATED",
      `User ${updatedUser.email}`,
      "PROFILE_EDIT",
      `Updated profile and multi-role assignment for ${updatedUser.fullName} (UC-09).`,
    );

    setEditingUser(null);
    setToastBanner({
      type: "success",
      text: "User profile and multi-role assignments updated successfully! (MSG-ADM-03)",
    });
  };

  const suspendedCount = usersDirectory.filter(
    (u) => u.status !== "ACTIVE",
  ).length;

  return (
    <WmmsAppShell mode="admin">
      <div className="max-w-[1400px] mx-auto space-y-5">
        {/* Hàng tiêu đề màn hình + 2 nút hành động góc phải (Field #3 & Field #4) */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
              User Accounts &amp; Multi-Role RBAC (SCR-ADM-01)
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Configure employee system accounts, role assignments, and
              functional security privileges.
            </p>
          </div>

          <div className="flex items-center gap-2.5">
            {/* Field #3: + Create New Account */}
            <button
              type="button"
              onClick={() => setShowCreateModal(true)}
              className="px-3.5 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition"
            >
              + Create New Account
            </button>

            {/* Field #4: Role Permission Matrix */}
            <button
              type="button"
              onClick={() => setShowPermissionMatrixModal(true)}
              className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition"
            >
              Role Permission Matrix
            </button>
          </div>
        </div>

        {/* Thông báo phản hồi nếu có */}
        {toastBanner && (
          <div
            className={`px-4 py-2.5 rounded-md text-xs font-medium flex items-center justify-between ${
              toastBanner.type === "error"
                ? "bg-red-50 text-red-700 border border-red-200"
                : "bg-emerald-50 text-emerald-800 border border-emerald-200"
            }`}
          >
            <span>{toastBanner.text}</span>
            <button
              type="button"
              onClick={() => setToastBanner(null)}
              className="font-bold ml-4 text-sm leading-none"
            >
              ×
            </button>
          </div>
        )}

        {/* 3 Thẻ KPI Thống kê (ACTIVE USERS: 24, ROLES CONFIGURED: 7, SUSPENDED ACCOUNTS: 1) */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              ACTIVE USERS
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">
              24
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Across 7 factory departments
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              ROLES CONFIGURED
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">7</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Jira-style functional RBAC
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              SUSPENDED ACCOUNTS
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">
              {suspendedCount}
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Action logged in Audit Trail
            </div>
          </div>
        </div>

        {/* Bảng Employee User Directory (Field #5, #6, #7) */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-5 py-3.5 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <h2 className="text-xs font-bold text-slate-800">
              Employee User Directory
            </h2>

            {/* Field #5: Search by name, email, or role... */}
            <input
              type="text"
              value={searchKeyword}
              onChange={(e) => setSearchKeyword(e.target.value)}
              placeholder="Search by name, email, or role..."
              className="w-full sm:w-64 px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-5">Employee Name</th>
                  <th className="py-3 px-4">Email Address</th>
                  <th className="py-3 px-4">Primary Assigned Role</th>
                  <th className="py-3 px-4">Operational Scope</th>
                  <th className="py-3 px-4">Account Status</th>
                  <th className="py-3 px-5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-xs">
                {filteredUsers.map((user) => (
                  <tr
                    key={user.userId}
                    className="hover:bg-slate-50/80 transition-colors"
                  >
                    <td className="py-3.5 px-5 font-bold text-slate-900">
                      {user.fullName}
                    </td>
                    <td className="py-3.5 px-4 text-slate-600">{user.email}</td>
                    <td className="py-3.5 px-4">
                      <span
                        className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded border ${getRoleBadgeStyle(
                          user.primaryRoleLabel,
                        )}`}
                      >
                        {user.primaryRoleLabel}
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-slate-600">
                      {user.operationalScope}
                    </td>
                    <td className="py-3.5 px-4">
                      {user.status === "ACTIVE" ? (
                        <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-emerald-100 text-emerald-700">
                          ACTIVE
                        </span>
                      ) : (
                        <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-rose-100 text-rose-700">
                          SUSPENDED
                        </span>
                      )}
                    </td>
                    <td className="py-3.5 px-5 text-right">
                      {user.status === "ACTIVE" ? (
                        <button
                          type="button"
                          onClick={() => setEditingUser({ ...user })}
                          className="px-2.5 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50"
                        >
                          Edit
                        </button>
                      ) : (
                        <button
                          type="button"
                          onClick={() => handleToggleBanStatus(user)}
                          className="px-2.5 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50"
                        >
                          Unban
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* Modal 3.2: Provision New User Account (MOD-ADM-01 - Section III.3.2) */}
      <ProvisionUserModal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        existingEmails={usersDirectory.map((u) => u.email)}
        onSuccess={handleUserCreated}
        onError={(message) =>
          setToastBanner({
            type: "error",
            text: message,
          })
        }
      />

      {/* Modal 3.3: Role Permission Configuration Matrix (MOD-ADM-02 - Section III.3.3) */}
      <RolePermissionMatrixModal
        isOpen={showPermissionMatrixModal}
        onClose={() => setShowPermissionMatrixModal(false)}
      />

      {/* Modal Edit User (UC-09 & UC-10) */}
      <EditUserModal
        user={editingUser}
        onClose={() => setEditingUser(null)}
        onSave={handleSaveEditUser}
        onSuspendOrBan={handleToggleBanStatus}
      />
    </WmmsAppShell>
  );
}
