import { Fragment, useMemo, useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { adminApi } from "../api/userApi";
import { useUsers } from "../hooks/useUsers";
import type { UserDto } from "../types";
import ProvisionUserModal from "../components/ProvisionUserModal";
import RolePermissionMatrixModal from "../components/RolePermissionMatrixModal";
import EditUserModal from "../components/EditUserModal";
import { userStatusLabel } from "../../../lib/statusLabels";

interface ToastBanner {
  type: "error" | "success";
  text: string;
}

function getInitials(name?: string, username?: string): string {
  const source = (name || username || "U").trim();
  const parts = source.split(/\s+/);
  if (parts.length >= 2) {
    return (
      parts[parts.length - 2][0] + parts[parts.length - 1][0]
    ).toUpperCase();
  }
  return source.slice(0, 2).toUpperCase();
}

export default function UserManagementPage() {
  const queryClient = useQueryClient();

  const [searchInput, setSearchInput] = useState("");
  const [appliedKeyword, setAppliedKeyword] = useState("");
  const [roleFilter, setRoleFilter] = useState("ALL");
  const [statusFilter, setStatusFilter] = useState("ALL");

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const [toastBanner, setToastBanner] = useState<ToastBanner | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);
  const [showPermissionMatrixModal, setShowPermissionMatrixModal] =
    useState(false);

  const {
    data: usersPaged,
    isLoading,
    isFetching,
    isError,
    error,
  } = useUsers({
    searchKeyword: appliedKeyword.trim() || undefined,
    pageNumber,
    pageSize,
  });

  const rawUsersList: UserDto[] = usersPaged?.items ?? [];
  const totalCount = usersPaged?.totalCount ?? 0;
  const totalPages = Math.max(
    1,
    usersPaged?.totalPages || Math.ceil(totalCount / pageSize),
  );
  const startItem = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endItem = Math.min(pageNumber * pageSize, totalCount);

  const usersList = useMemo(() => {
    return rawUsersList.filter((u) => {
      const matchRole =
        roleFilter === "ALL" ||
        (u.roles &&
          u.roles.some((r) => r.toUpperCase().includes(roleFilter)));
      const isSuspended =
        u.status === "BANNED" || u.status === "SUSPENDED";
      const matchStatus =
        statusFilter === "ALL" ||
        (statusFilter === "ACTIVE" && u.status === "ACTIVE") ||
        (statusFilter === "BANNED" && isSuspended);
      return matchRole && matchStatus;
    });
  }, [rawUsersList, roleFilter, statusFilter]);

  const suspendedCount = useMemo(
    () =>
      rawUsersList.filter(
        (u) => u.status === "BANNED" || u.status === "SUSPENDED",
      ).length,
    [rawUsersList],
  );

  const getRoleBadgeStyle = (roleLabel: string) => {
    const upper = String(roleLabel || "").toUpperCase();
    if (upper.includes("ADMIN"))
      return "bg-purple-50 text-purple-700 border-purple-200";
    if (upper.includes("OWNER"))
      return "bg-amber-50 text-amber-800 border-amber-200";
    if (upper.includes("SALES"))
      return "bg-blue-50 text-blue-700 border-blue-200";
    if (upper.includes("PLANNER"))
      return "bg-indigo-50 text-indigo-700 border-indigo-200";
    if (upper.includes("LEADER"))
      return "bg-orange-50 text-orange-800 border-orange-200";
    if (upper.includes("QC"))
      return "bg-rose-50 text-rose-700 border-rose-200";
    if (upper.includes("WAREHOUSE"))
      return "bg-cyan-50 text-cyan-800 border-cyan-200";
    if (upper.includes("PURCHASING"))
      return "bg-emerald-50 text-emerald-700 border-emerald-200";
    return "bg-slate-100 text-slate-700 border-slate-200";
  };

  const toggleBanMutation = useMutation({
    mutationFn: async (user: UserDto) => {
      const isCurrentlyActive = user.status === "ACTIVE";
      const apiStatus = isCurrentlyActive ? "BANNED" : "ACTIVE";
      const reason = `Quản trị viên cập nhật trạng thái người dùng`;
      return await adminApi.updateUserStatus(user.userId, apiStatus, reason);
    },
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
      const msg = res?.message || "Cập nhật trạng thái tài khoản thành công.";
      setToastBanner({ type: "success", text: msg });
      toast.success(msg);
    },
    onError: (err: unknown) => {
      const msg =
        (err as { message?: string })?.message ||
        "Cập nhật trạng thái người dùng thất bại.";
      setToastBanner({ type: "error", text: msg });
      toast.error(msg);
    },
  });

  const handleSearchSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setAppliedKeyword(searchInput.trim());
    setPageNumber(1);
  };

  const handleClearSearch = () => {
    setSearchInput("");
    setAppliedKeyword("");
    setPageNumber(1);
  };

  return (
    <WmmsAppShell mode="admin">
      <div className="max-w-[1400px] mx-auto space-y-6">
        {/* Page Heading + Action Buttons */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
              Tài khoản nhân sự &amp; Phân quyền
            </h1>
            <p className="text-sm text-slate-600 mt-1">
              Quản lý danh sách tài khoản nhân viên, thiết lập vai trò phòng ban và phân quyền truy cập hệ thống.
            </p>
          </div>

          <div className="flex items-center gap-3 shrink-0">
            <button
              type="button"
              onClick={() => setShowPermissionMatrixModal(true)}
              className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
            >
              <svg
                className="w-4 h-4 text-slate-500 shrink-0"
                fill="none"
                stroke="currentColor"
                strokeWidth={2}
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M4 6h16M4 10h16M4 14h16M4 18h16"
                />
              </svg>
              <span>Ma trận phân quyền</span>
            </button>

            <button
              type="button"
              onClick={() => setShowCreateModal(true)}
              className="px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
            >
              <svg
                className="w-4 h-4 shrink-0"
                fill="none"
                stroke="currentColor"
                strokeWidth={2.5}
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M12 4v16m8-8H4"
                />
              </svg>
              <span>Tạo tài khoản mới</span>
            </button>
          </div>
        </div>

        {/* Feedback Banner */}
        {toastBanner && (
          <div
            className={`px-4 py-3 rounded-lg text-sm font-medium flex items-center justify-between shadow-xs ${
              toastBanner.type === "error"
                ? "bg-rose-50 text-rose-700 border border-rose-200"
                : "bg-emerald-50 text-emerald-800 border border-emerald-200"
            }`}
          >
            <span>{toastBanner.text}</span>
            <button
              type="button"
              onClick={() => setToastBanner(null)}
              className="font-bold ml-4 text-base leading-none cursor-pointer"
            >
              ×
            </button>
          </div>
        )}

        {/* 3 KPI Summary Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                TỔNG SỐ TÀI KHOẢN
              </span>
              <span className="w-8 h-8 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center">
                <svg
                  className="w-4 h-4"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth={2}
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"
                  />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">
              {isLoading ? "..." : totalCount}
            </div>
            <div className="text-xs text-slate-500 mt-1">
              Trên toàn bộ phòng ban xưởng
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                VAI TRÒ HỆ THỐNG
              </span>
              <span className="w-8 h-8 rounded-lg bg-purple-50 text-purple-600 flex items-center justify-center">
                <svg
                  className="w-4 h-4"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth={2}
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"
                  />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">
              8 vai trò
            </div>
            <div className="text-xs text-slate-500 mt-1">
              Phân quyền theo chức năng chuyên biệt
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                TÀI KHOẢN TẠM KHÓA
              </span>
              <span className="w-8 h-8 rounded-lg bg-rose-50 text-rose-600 flex items-center justify-center">
                <svg
                  className="w-4 h-4"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth={2}
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"
                  />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">
              {isLoading ? "..." : suspendedCount}
            </div>
            <div className="text-xs text-slate-500 mt-1">
              Tài khoản tạm ngưng truy cập
            </div>
          </div>
        </div>

        {/* Employee Directory Card */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          {/* Card Header + Search & Filters */}
          <div className="px-6 py-4 border-b border-slate-200 flex flex-col lg:flex-row lg:items-center justify-between gap-4 bg-white">
            <div className="flex items-center gap-2.5">
              <h2 className="text-base font-bold text-slate-900">
                Danh sách tài khoản nhân viên
              </h2>
              {isFetching && !isLoading && (
                <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-600 border border-blue-200">
                  <span className="w-2 h-2 rounded-full border border-blue-600 border-t-transparent animate-spin" />
                  Đang đồng bộ...
                </span>
              )}
            </div>

            <div className="flex flex-wrap items-center gap-3">
              {/* Role Filter */}
              <select
                value={roleFilter}
                onChange={(e) => setRoleFilter(e.target.value)}
                aria-label="Lọc theo vai trò"
                className="px-3 py-2 text-sm bg-white border border-slate-300 rounded-lg text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-600"
              >
                <option value="ALL">Tất cả vai trò</option>
                <option value="ADMIN">Quản trị viên (ADMIN)</option>
                <option value="OWNER">Chủ xưởng (OWNER)</option>
                <option value="SALES">Kinh doanh (SALES)</option>
                <option value="PLANNER">Kế hoạch (PLANNER)</option>
                <option value="LEADER">Quản đốc (LEADER)</option>
                <option value="QC">Kiểm soát chất lượng (QC)</option>
                <option value="WAREHOUSE">Thủ kho (WAREHOUSE)</option>
                <option value="PURCHASING">Mua hàng (PURCHASING)</option>
              </select>

              {/* Status Filter */}
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                aria-label="Lọc theo trạng thái"
                className="px-3 py-2 text-sm bg-white border border-slate-300 rounded-lg text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-600"
              >
                <option value="ALL">Tất cả trạng thái</option>
                <option value="ACTIVE">Đang hoạt động</option>
                <option value="BANNED">Đã tạm khóa</option>
              </select>

              {/* Search Input + Submit */}
              <form
                onSubmit={handleSearchSubmit}
                className="flex items-center gap-2 w-full sm:w-auto"
              >
                <div className="relative w-full sm:w-72">
                  <svg
                    className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth={2}
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
                    />
                  </svg>
                  <input
                    type="text"
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    placeholder="Tìm theo tên, username, email..."
                    className="w-full pl-9 pr-8 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
                  />
                  {searchInput && (
                    <button
                      type="button"
                      onClick={handleClearSearch}
                      className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-sm font-bold px-1 cursor-pointer"
                      title="Xóa tìm kiếm"
                    >
                      ×
                    </button>
                  )}
                </div>

                <button
                  type="submit"
                  disabled={isFetching}
                  className="px-4 py-2 text-sm font-semibold text-slate-700 bg-white border border-slate-300 hover:bg-slate-50 disabled:opacity-60 rounded-lg shadow-xs transition whitespace-nowrap cursor-pointer"
                >
                  Tìm kiếm
                </button>
              </form>
            </div>
          </div>

          {/* Data Table */}
          <div className="overflow-x-auto bg-white">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-6 min-w-[220px]">Hồ sơ nhân viên</th>
                  <th className="py-3.5 px-4 min-w-[140px]">Tên đăng nhập</th>
                  <th className="py-3.5 px-4 min-w-[200px]">Thông tin liên hệ</th>
                  <th className="py-3.5 px-4 min-w-[150px]">Vai trò đảm nhiệm</th>
                  <th className="py-3.5 px-4 min-w-[160px]">Trạng thái</th>
                  <th className="py-3.5 px-6 text-right min-w-[150px]">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm bg-white">
                {isFetching && (
                  <tr>
                    <td
                      colSpan={6}
                      className="py-12 text-center text-slate-500"
                    >
                      <div className="inline-block w-6 h-6 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                      <p className="text-sm font-medium text-slate-600">
                        Đang tải danh sách nhân viên...
                      </p>
                    </td>
                  </tr>
                )}

                {!isFetching && isError && (
                  <tr>
                    <td
                      colSpan={6}
                      className="py-10 text-center text-rose-600 font-medium text-sm"
                    >
                      {(error as { message?: string })?.message ||
                        "Không tải được danh sách nhân viên. Vui lòng kiểm tra kết nối hoặc phiên đăng nhập."}
                    </td>
                  </tr>
                )}

                {!isFetching && !isError && usersList.length === 0 && (
                  <tr>
                    <td
                      colSpan={6}
                      className="py-10 text-center text-slate-500 text-sm"
                    >
                      Không tìm thấy tài khoản nhân viên nào phù hợp với điều kiện lọc.
                    </td>
                  </tr>
                )}

                {!isFetching &&
                  !isError &&
                  usersList.map((user) => {
                    const isActive = user.status === "ACTIVE";
                    const initials = getInitials(user.fullName, user.username);

                    return (
                      <tr
                        key={user.userId}
                        className="hover:bg-slate-50/90 transition-colors"
                      >
                        <td className="py-4 px-6">
                          <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-full bg-slate-100 border border-slate-200 text-slate-700 font-bold text-xs flex items-center justify-center shrink-0">
                              {initials}
                            </div>
                            <div className="whitespace-nowrap">
                              <div className="font-semibold text-slate-900 text-sm">
                                {user.fullName || "—"}
                              </div>
                              <div className="text-xs text-slate-500">
                                {user.username}
                              </div>
                            </div>
                          </div>
                        </td>

                        <td className="py-4 px-4 font-mono text-sm font-medium text-slate-700 whitespace-nowrap">
                          {user.username}
                        </td>

                        <td className="py-4 px-4 whitespace-nowrap">
                          <div className="text-slate-700 text-sm">
                            {user.email}
                          </div>
                          <div className="text-xs font-mono text-slate-500 mt-0.5">
                            {user.phone || "—"}
                          </div>
                        </td>

                        <td className="py-4 px-4">
                          <div className="flex flex-wrap gap-1.5">
                            {user.roles && user.roles.length > 0 ? (
                              user.roles.map((r) => (
                                <span
                                  key={r}
                                  className={`inline-block px-2.5 py-0.5 text-xs font-bold uppercase rounded border whitespace-nowrap ${getRoleBadgeStyle(
                                    r,
                                  )}`}
                                >
                                  {r}
                                </span>
                              ))
                            ) : (
                              <span className="text-slate-400 italic text-xs">
                                Chưa gán vai trò
                              </span>
                            )}
                          </div>
                        </td>

                        <td className="py-4 px-4 whitespace-nowrap">
                          {isActive ? (
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 whitespace-nowrap">
                              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 shrink-0" />
                              ĐANG HOẠT ĐỘNG
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-rose-50 text-rose-700 border border-rose-200 whitespace-nowrap">
                              <span className="w-1.5 h-1.5 rounded-full bg-rose-500 shrink-0" />
                              {user.status === "BANNED" ||
                              user.status === "SUSPENDED"
                                ? "ĐÃ TẠM KHÓA"
                                : userStatusLabel(user.status)}
                            </span>
                          )}
                        </td>

                        <td className="py-4 px-6 text-right whitespace-nowrap">
                          <div className="inline-flex items-center gap-2">
                            <button
                              type="button"
                              onClick={() => setEditingUser(user)}
                              className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition cursor-pointer"
                            >
                              Chỉnh sửa
                            </button>

                            {isActive ? (
                              <button
                                type="button"
                                disabled={toggleBanMutation.isPending}
                                onClick={() => toggleBanMutation.mutate(user)}
                                className="px-3 py-1.5 text-xs font-semibold text-rose-700 bg-rose-50 border border-rose-200 rounded-lg hover:bg-rose-100 transition cursor-pointer disabled:opacity-50"
                              >
                                Khóa
                              </button>
                            ) : (
                              <button
                                type="button"
                                disabled={toggleBanMutation.isPending}
                                onClick={() => toggleBanMutation.mutate(user)}
                                className="px-3 py-1.5 text-xs font-semibold text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-lg hover:bg-emerald-100 transition cursor-pointer disabled:opacity-50"
                              >
                                Mở khóa
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer */}
          <div className="px-6 py-4 border-t border-slate-200 bg-[#F8FAFC] flex flex-col sm:flex-row sm:items-center justify-between gap-4 text-sm text-slate-600">
            <div className="flex flex-wrap items-center gap-3">
              <span>
                Hiển thị <strong className="text-slate-900">{startItem}</strong>{" "}
                đến <strong className="text-slate-900">{endItem}</strong> trong
                tổng số <strong className="text-slate-900">{totalCount}</strong>{" "}
                tài khoản
              </span>

              <div className="flex items-center gap-2 sm:ml-2 sm:border-l sm:border-slate-200 sm:pl-3">
                <span className="text-slate-500 text-xs">Số dòng:</span>
                <select
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setPageNumber(1);
                  }}
                  aria-label="Số dòng mỗi trang"
                  className="px-2.5 py-1 bg-white border border-slate-300 rounded-lg text-xs font-semibold text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-600"
                >
                  <option value={10}>10</option>
                  <option value={20}>20</option>
                  <option value={50}>50</option>
                </select>
              </div>
            </div>

            <div className="flex items-center gap-1.5">
              <button
                type="button"
                disabled={pageNumber <= 1 || isFetching}
                onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition cursor-pointer"
              >
                Trước
              </button>

              <div className="flex items-center gap-1 px-1">
                {Array.from({ length: totalPages }, (_, i) => i + 1)
                  .filter(
                    (p) =>
                      p === 1 ||
                      p === totalPages ||
                      Math.abs(p - pageNumber) <= 1,
                  )
                  .map((p, idx, arr) => (
                    <Fragment key={p}>
                      {idx > 0 && arr[idx - 1] !== p - 1 && (
                        <span className="px-1 text-slate-400 text-xs">...</span>
                      )}
                      <button
                        type="button"
                        disabled={isFetching}
                        onClick={() => setPageNumber(p)}
                        className={`w-8 h-8 text-xs font-bold rounded-lg transition cursor-pointer ${
                          p === pageNumber
                            ? "bg-blue-600 text-white shadow-xs"
                            : "text-slate-700 bg-white border border-slate-300 hover:bg-slate-50"
                        }`}
                      >
                        {p}
                      </button>
                    </Fragment>
                  ))}
              </div>

              <button
                type="button"
                disabled={pageNumber >= totalPages || isFetching}
                onClick={() =>
                  setPageNumber((p) => Math.min(totalPages, p + 1))
                }
                className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition cursor-pointer"
              >
                Sau
              </button>
            </div>
          </div>
        </div>
      </div>

      <ProvisionUserModal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        onSuccess={(msg) => {
          setToastBanner({ type: "success", text: msg });
          toast.success(msg);
        }}
        onError={(msg) => {
          setToastBanner({ type: "error", text: msg });
          toast.error(msg);
        }}
      />

      <RolePermissionMatrixModal
        isOpen={showPermissionMatrixModal}
        onClose={() => setShowPermissionMatrixModal(false)}
      />

      <EditUserModal
        user={editingUser}
        onClose={() => setEditingUser(null)}
        onSuccess={(msg) => {
          setToastBanner({ type: "success", text: msg });
          toast.success(msg);
        }}
        onError={(msg) => {
          setToastBanner({ type: "error", text: msg });
          toast.error(msg);
        }}
      />
    </WmmsAppShell>
  );
}

