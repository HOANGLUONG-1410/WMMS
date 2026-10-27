import { useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { adminApi } from "../api/userApi";
import { useUsers } from "../hooks/useUsers";
import type { UserDto } from "../types";
import ProvisionUserModal from "../components/ProvisionUserModal";
import RolePermissionMatrixModal from "../components/RolePermissionMatrixModal";
import EditUserModal from "../components/EditUserModal";

interface ToastBanner {
  type: "error" | "success";
  text: string;
}

/** Ánh xạ mã role kỹ thuật từ backend sang nhãn hiển thị và phạm vi nghiệp vụ */
function formatRoleInfo(roles: string[] = []) {
  const primary = roles[0]?.toUpperCase() || "STAFF";

  switch (primary) {
    case "ADMIN":
      return {
        label: "SYSTEM ADMIN",
        scope: "Global System Configuration, RBAC & Audit Trail",
      };
    case "OWNER":
      return {
        label: "WORKSHOP OWNER",
        scope: "BOM Breakdown, Purchasing Approval, Dispatch Release",
      };
    case "SALES":
      return {
        label: "SALES STAFF",
        scope: "Excel PO Staging, CRUD Sanitization, Space Init",
      };
    case "PLANNER":
      return {
        label: "PRODUCTION PLANNER",
        scope: "Material Routing, Cutting List Optimization, Work Orders",
      };
    case "PURCHASING":
      return {
        label: "PURCHASING STAFF",
        scope: "Vendor Sourcing, PO Creation & ETA Management",
      };
    case "WAREHOUSE":
      return {
        label: "WAREHOUSE KEEPER",
        scope: "Goods Receipt Commit, Goods Issue Floor Handover",
      };
    case "LEADER":
      return {
        label: "PRODUCTION LEADER",
        scope: "Kanban Floor Dispatch, Overdriver Requests",
      };
    case "QC":
      return {
        label: "QC STAFF",
        scope: "Workstation Quality Gate, Defect Logging",
      };
    default:
      return {
        label: primary,
        scope: "Standard Operational Privileges",
      };
  }
}

/**
 * Màn hình III.3: System Administration
 * Bao gồm 3 giao diện chuẩn theo đúng thiết kế trong Report 3:
 * - 3.1: User Accounts & Multi-Role RBAC (SCR-ADM-01) - Fetch thực từ GET /users
 * - 3.2: Provision New User Account Modal (MOD-ADM-01) -> ProvisionUserModal.tsx
 * - 3.3: Role Permission Configuration Matrix Modal (MOD-ADM-02) -> RolePermissionMatrixModal.tsx
 */
export default function UserManagementPage() {
  const queryClient = useQueryClient();

  // Từ khóa tìm kiếm trên bảng Employee User Directory (Field #5)
  const [searchKeyword, setSearchKeyword] = useState("");
  const [toastBanner, setToastBanner] = useState<ToastBanner | null>(null);

  // State cho Modal 3.2: Provision New User Account (MOD-ADM-01)
  const [showCreateModal, setShowCreateModal] = useState(false);

  // State cho Modal chỉnh sửa tài khoản nhân sự (UC-09)
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);

  // State cho Modal 3.3: Role Permission Configuration Matrix (MOD-ADM-02)
  const [showPermissionMatrixModal, setShowPermissionMatrixModal] =
    useState(false);

  // Fetch danh sách tài khoản nhân sự từ backend API /api/v1/users
  const {
    data: usersPaged,
    isLoading,
    isError,
    error,
  } = useUsers({
    searchTerm: searchKeyword.trim() || undefined,
  });

  const usersList: UserDto[] = usersPaged?.items ?? [];

  // Thống kê nhanh từ dữ liệu thật
  const totalUsersCount = usersPaged?.totalCount ?? usersList.length;
  const suspendedCount = useMemo(
    () =>
      usersList.filter(
        (u) => u.status === "BANNED" || u.status === "SUSPENDED",
      ).length,
    [usersList],
  );

  // Ánh xạ màu sắc huy hiệu Primary Assigned Role giống hệt ảnh SCR-ADM-01
  const getRoleBadgeStyle = (roleLabel: string) => {
    const upper = String(roleLabel || "").toUpperCase();
    if (upper.includes("OWNER"))
      return "bg-yellow-100 text-amber-800 border-yellow-200";
    if (upper.includes("ADMIN"))
      return "bg-slate-700 text-white border-slate-800";
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

  // Mutation Khóa / Mở khóa tài khoản trực tiếp từ bảng (UC-10)
  const toggleBanMutation = useMutation({
    mutationFn: async (user: UserDto) => {
      const isCurrentlyActive = user.status === "ACTIVE";
      const apiStatus = isCurrentlyActive ? "BANNED" : "ACTIVE";
      const reason = `Admin toggled status via SCR-ADM-01`;
      return await adminApi.updateUserStatus(user.userId, apiStatus, reason);
    },
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
      setToastBanner({
        type: "success",
        text: res?.message || "Account status updated successfully (UC-10).",
      });
    },
    onError: (err: unknown) => {
      setToastBanner({
        type: "error",
        text:
          (err as { message?: string })?.message ||
          "Failed to update user status.",
      });
    },
  });

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

        {/* 3 Thẻ KPI Thống kê (ACTIVE USERS, ROLES CONFIGURED, SUSPENDED ACCOUNTS) */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              ACTIVE USERS
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">
              {isLoading ? "..." : totalUsersCount}
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Across factory departments
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
              {isLoading ? "..." : suspendedCount}
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
                {isLoading && (
                  <tr>
                    <td
                      colSpan={6}
                      className="py-10 text-center text-slate-400"
                    >
                      <div className="inline-block w-6 h-6 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                      <p className="text-xs">
                        Fetching users from WMMS backend...
                      </p>
                    </td>
                  </tr>
                )}

                {isError && (
                  <tr>
                    <td
                      colSpan={6}
                      className="py-8 text-center text-rose-600 font-medium"
                    >
                      {(error as { message?: string })?.message ||
                        "Unable to load employee directory. Please check network or session."}
                    </td>
                  </tr>
                )}

                {!isLoading && !isError && usersList.length === 0 && (
                  <tr>
                    <td
                      colSpan={6}
                      className="py-8 text-center text-slate-400 italic"
                    >
                      No user accounts found matching your search.
                    </td>
                  </tr>
                )}

                {!isLoading &&
                  !isError &&
                  usersList.map((user) => {
                    const roleInfo = formatRoleInfo(user.roles);
                    const isActive = user.status === "ACTIVE";

                    return (
                      <tr
                        key={user.userId}
                        className="hover:bg-slate-50/80 transition-colors"
                      >
                        <td className="py-3.5 px-5 font-bold text-slate-900">
                          {user.fullName || user.username}
                        </td>
                        <td className="py-3.5 px-4 text-slate-600">
                          {user.email}
                        </td>
                        <td className="py-3.5 px-4">
                          <span
                            className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded border ${getRoleBadgeStyle(
                              roleInfo.label,
                            )}`}
                          >
                            {roleInfo.label}
                          </span>
                        </td>
                        <td className="py-3.5 px-4 text-slate-600">
                          {roleInfo.scope}
                        </td>
                        <td className="py-3.5 px-4">
                          {isActive ? (
                            <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-emerald-100 text-emerald-700">
                              ACTIVE
                            </span>
                          ) : (
                            <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-rose-100 text-rose-700">
                              {user.status || "SUSPENDED"}
                            </span>
                          )}
                        </td>
                        <td className="py-3.5 px-5 text-right">
                          {isActive ? (
                            <button
                              type="button"
                              onClick={() => setEditingUser(user)}
                              className="px-2.5 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition"
                            >
                              Edit
                            </button>
                          ) : (
                            <button
                              type="button"
                              disabled={toggleBanMutation.isPending}
                              onClick={() => toggleBanMutation.mutate(user)}
                              className="px-2.5 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition"
                            >
                              Unban
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* Modal 3.2: Provision New User Account (MOD-ADM-01 - Section III.3.2) */}
      <ProvisionUserModal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        onSuccess={(msg) =>
          setToastBanner({
            type: "success",
            text: msg,
          })
        }
        onError={(msg) =>
          setToastBanner({
            type: "error",
            text: msg,
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
        onSuccess={(msg) =>
          setToastBanner({
            type: "success",
            text: msg,
          })
        }
        onError={(msg) =>
          setToastBanner({
            type: "error",
            text: msg,
          })
        }
      />
    </WmmsAppShell>
  );
}
