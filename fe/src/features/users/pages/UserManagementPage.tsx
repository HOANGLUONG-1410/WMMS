import { Fragment, useMemo, useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
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


export default function UserManagementPage() {
  const queryClient = useQueryClient();

  // State tìm kiếm: searchInput (khi người dùng gõ) vs appliedKeyword (khi bấm Search/Enter)
  const [searchInput, setSearchInput] = useState("");
  const [appliedKeyword, setAppliedKeyword] = useState("");

  // State phân trang
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const [toastBanner, setToastBanner] = useState<ToastBanner | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);
  const [showPermissionMatrixModal, setShowPermissionMatrixModal] =
    useState(false);

  // Fetch danh sách tài khoản nhân sự từ backend API /api/v1/users
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

  const usersList: UserDto[] = usersPaged?.items ?? [];
  const totalCount = usersPaged?.totalCount ?? 0;
  const totalPages = Math.max(
    1,
    usersPaged?.totalPages || Math.ceil(totalCount / pageSize),
  );
  const startItem = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endItem = Math.min(pageNumber * pageSize, totalCount);

  // Thống kê nhanh từ dữ liệu thật
  const suspendedCount = useMemo(
    () =>
      usersList.filter(
        (u) => u.status === "BANNED" || u.status === "SUSPENDED",
      ).length,
    [usersList],
  );

  // Ánh xạ màu sắc huy hiệu Primary Assigned Role
  const getRoleBadgeStyle = (roleLabel: string) => {
    const upper = String(roleLabel || "").toUpperCase();
    if (upper.includes("OWNER"))
      return "bg-yellow-100 text-amber-800 border-yellow-200";
    if (upper.includes("ADMIN"))
      return "bg-slate-700 text-white border-slate-800";
    if (upper.includes("SALES"))
      return "bg-blue-100 text-blue-700 border-blue-200";
    if (upper.includes("PLANNER"))
      return "bg-indigo-100 text-indigo-700 border-indigo-200";
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

  // Mutation Khóa / Mở khóa tài khoản trực tiếp từ bảng
  const toggleBanMutation = useMutation({
    mutationFn: async (user: UserDto) => {
      const isCurrentlyActive = user.status === "ACTIVE";
      const apiStatus = isCurrentlyActive ? "BANNED" : "ACTIVE";
      const reason = `Quản trị viên cập nhật trạng thái người dùng`;
      return await adminApi.updateUserStatus(user.userId, apiStatus, reason);
    },
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["users"] });
      setToastBanner({
        type: "success",
        text: res?.message || "Cập nhật trạng thái tài khoản thành công.",
      });
    },
    onError: (err: unknown) => {
      setToastBanner({
        type: "error",
        text:
          (err as { message?: string })?.message ||
          "Cập nhật trạng thái người dùng thất bại.",
      });
    },
  });

  // Xử lý xác nhận tìm kiếm khi bấm nút hoặc gõ phím Enter
  const handleSearchSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setAppliedKeyword(searchInput.trim());
    setPageNumber(1);
  };

  // Xử lý xóa từ khóa tìm kiếm và trở về toàn bộ danh sách
  const handleClearSearch = () => {
    setSearchInput("");
    setAppliedKeyword("");
    setPageNumber(1);
  };

  return (
    <WmmsAppShell mode="admin">
      <div className="max-w-[1400px] mx-auto space-y-5">
        {/* Hàng tiêu đề màn hình + 2 nút hành động góc phải */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
              Tài khoản người dùng &amp; phân quyền đa vai trò
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Cấu hình tài khoản hệ thống của nhân viên, gán vai trò và
              phân quyền chức năng bảo mật.
            </p>
          </div>

          <div className="flex items-center gap-2.5">
            <button
              type="button"
              onClick={() => setShowCreateModal(true)}
              className="px-3.5 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition"
            >
              + Tạo tài khoản mới
            </button>

            <button
              type="button"
              onClick={() => setShowPermissionMatrixModal(true)}
              className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition"
            >
              Ma trận phân quyền vai trò
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
              TỔNG SỐ NGƯỜI DÙNG
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">
              {isLoading ? "..." : totalCount}
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Trên toàn bộ các bộ phận của xưởng
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              VAI TRÒ ĐÃ CẤU HÌNH
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">8</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Bảo mật theo vai trò chức năng
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              BỊ KHÓA TRONG DANH SÁCH HIỆN TẠI
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">
              {isLoading ? "..." : suspendedCount}
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Tài khoản bị tạm khóa / cấm
            </div>
          </div>
        </div>

        {/* Bảng Employee User Directory */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-5 py-3.5 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <h2 className="text-xs font-bold text-slate-800">
                Danh bạ tài khoản nhân viên
              </h2>
              {isFetching && !isLoading && (
                <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[10px] font-medium bg-blue-50 text-blue-600 border border-blue-200">
                  <span className="w-2 h-2 rounded-full border border-blue-600 border-t-transparent animate-spin" />
                  Đang gọi máy chủ...
                </span>
              )}
            </div>

            {/* Form Tìm kiếm có nút bấm xác nhận, không gọi API onChange */}
            <form
              onSubmit={handleSearchSubmit}
              className="flex items-center gap-2 w-full sm:w-auto"
            >
              <div className="relative w-full sm:w-64">
                <input
                  type="text"
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  placeholder="Tìm theo tên, email, tên đăng nhập, số điện thoại..."
                  className="w-full pl-3 pr-7 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                {searchInput && (
                  <button
                    type="button"
                    onClick={handleClearSearch}
                    className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-xs font-bold px-1"
                    title="Xóa tìm kiếm"
                  >
                    ×
                  </button>
                )}
              </div>

              <button
                type="submit"
                disabled={isFetching}
                className="px-3.5 py-1.5 text-xs font-semibold text-white bg-[#2563eb] hover:bg-blue-700 disabled:opacity-60 rounded-md shadow-sm transition whitespace-nowrap"
              >
                Tìm kiếm
              </button>
            </form>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-5">Tên nhân viên</th>
                  <th className="py-3 px-4">Tên đăng nhập</th>
                  <th className="py-3 px-4">Địa chỉ email</th>
                  <th className="py-3 px-4">Điện thoại</th>
                  <th className="py-3 px-4">Vai trò</th>
                  <th className="py-3 px-4">Trạng thái tài khoản</th>
                  <th className="py-3 px-5 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-xs">
                {/* Spinner Indicator hiển thị rõ ràng khi đang request Backend */}
                {isFetching && (
                  <tr>
                    <td
                      colSpan={7}
                      className="py-12 text-center text-slate-400"
                    >
                      <div className="inline-block w-6 h-6 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                      <p className="text-xs font-medium text-slate-500">
                        Đang tải danh sách người dùng từ máy chủ WMMS...
                      </p>
                    </td>
                  </tr>
                )}

                {!isFetching && isError && (
                  <tr>
                    <td
                      colSpan={7}
                      className="py-8 text-center text-rose-600 font-medium"
                    >
                      {(error as { message?: string })?.message ||
                        "Không tải được danh bạ nhân viên. Vui lòng kiểm tra mạng hoặc phiên đăng nhập."}
                    </td>
                  </tr>
                )}

                {!isFetching && !isError && usersList.length === 0 && (
                  <tr>
                    <td
                      colSpan={7}
                      className="py-8 text-center text-slate-400 italic"
                    >
                      Không tìm thấy tài khoản nào khớp với tìm kiếm.
                    </td>
                  </tr>
                )}

                {!isFetching &&
                  !isError &&
                  usersList.map((user) => {
                    const isActive = user.status === "ACTIVE";

                    return (
                      <tr
                        key={user.userId}
                        className="hover:bg-slate-50/80 transition-colors"
                      >
                        <td className="py-3.5 px-5 font-bold text-slate-900 align-top">
                          {user.fullName || "—"}
                        </td>
                        <td className="py-3.5 px-4 font-mono text-xs font-semibold text-slate-700 align-top">
                          {user.username}
                        </td>
                        <td className="py-3.5 px-4 text-slate-600 align-top">
                          {user.email}
                        </td>
                        <td className="py-3.5 px-4 text-slate-600 font-mono text-xs align-top">
                          {user.phone || "—"}
                        </td>
                        <td className="py-3.5 px-4 align-top">
                          <div className="flex flex-col items-start gap-1">
                            {user.roles && user.roles.length > 0 ? (
                              user.roles.map((r) => (
                                <span
                                  key={r}
                                  className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded border whitespace-nowrap ${getRoleBadgeStyle(
                                    r,
                                  )}`}
                                >
                                  {r}
                                </span>
                              ))
                            ) : (
                              <span className="text-slate-400 italic text-xs">Chưa có vai trò</span>
                            )}
                          </div>
                        </td>
                        <td className="py-3.5 px-4 align-top">
                          {isActive ? (
                            <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-emerald-100 text-emerald-700">
                              Hoạt động
                            </span>
                          ) : (
                            <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-rose-100 text-rose-700">
                              {userStatusLabel(user.status)}
                            </span>
                          )}
                        </td>
                        <td className="py-3.5 px-5 text-right align-top">
                          {isActive ? (
                            <button
                              type="button"
                              onClick={() => setEditingUser(user)}
                              className="px-2.5 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition"
                            >
                              Sửa
                            </button>
                          ) : (
                            <button
                              type="button"
                              disabled={toggleBanMutation.isPending}
                              onClick={() => toggleBanMutation.mutate(user)}
                              className="px-2.5 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition"
                            >
                              Bỏ cấm
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>

          {/* Thanh Phân trang (Pagination Footer) */}
          <div className="px-5 py-3 border-t border-slate-200 bg-slate-50/40 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs text-slate-600">
            {/* Thống kê dòng hiển thị & chọn PageSize */}
            <div className="flex items-center gap-3">
              <span>
                Hiển thị <strong className="text-slate-800">{startItem}</strong>{" "}
                đến <strong className="text-slate-800">{endItem}</strong> trên{" "}
                <strong className="text-slate-800">{totalCount}</strong> người dùng
              </span>

              <div className="flex items-center gap-1.5 ml-2 border-l border-slate-200 pl-3">
                <span className="text-slate-500 text-[11px]">Số dòng/trang:</span>
                <select
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setPageNumber(1);
                  }}
                  className="px-2 py-1 bg-white border border-slate-300 rounded text-xs text-slate-700 focus:outline-none focus:ring-1 focus:ring-blue-500"
                >
                  <option value={10}>10</option>
                  <option value={20}>20</option>
                  <option value={50}>50</option>
                </select>
              </div>
            </div>

            {/* Các nút bấm chuyển trang */}
            <div className="flex items-center gap-1">
              <button
                type="button"
                disabled={pageNumber <= 1 || isFetching}
                onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                className="px-2.5 py-1 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition"
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
                        className={`w-7 h-7 text-xs font-semibold rounded transition ${
                          p === pageNumber
                            ? "bg-[#2563eb] text-white shadow-sm"
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
                className="px-2.5 py-1 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition"
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

      <RolePermissionMatrixModal
        isOpen={showPermissionMatrixModal}
        onClose={() => setShowPermissionMatrixModal(false)}
      />

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
