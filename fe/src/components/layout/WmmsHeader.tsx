import { type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { useSession } from "../../features/auth/store/SessionContext";
import { useLogout } from "../../features/auth/hooks/useLogout";
import { ROLE_CONFIG } from "../../constants/wmmsConstants";

export type RoleCode = keyof typeof ROLE_CONFIG;

export interface WmmsHeaderProps {
  /**
   * Vùng nội dung tùy biến bên trái Header (VD: Nút quay lại danh sách đơn hàng `← Danh sách đơn hàng` hoặc tiêu đề ngữ cảnh)
   */
  customTopLeft?: ReactNode;

  /**
   * Callback mở ngăn kéo điều hướng di động (Mobile Hamburger Drawer)
   */
  onOpenMobileMenu?: () => void;

  /**
   * Vai trò ghi đè tùy chọn (nếu có)
   */
  activeRoleOverride?: RoleCode;

  /**
   * Lớp CSS tùy chỉnh cho thẻ header
   */
  className?: string;
}

const ROLES = {
  ADMIN: "ADMIN",
  OWNER: "OWNER",
  SALES: "SALES",
  PLANNER: "PLANNER",
  PURCHASING: "PURCHASING",
  WAREHOUSE: "WAREHOUSE",
  LEADER: "LEADER",
  QC: "QC",
} as const;

export default function WmmsHeader({
  customTopLeft = null,
  onOpenMobileMenu,
  activeRoleOverride,
  className = "",
}: WmmsHeaderProps) {
  const navigate = useNavigate();
  const { currentUser, switchActiveRole } = useSession();
  const logoutMutation = useLogout();

  // Xác định vai trò đang hoạt động (activeRole)
  const activeRole: RoleCode =
    activeRoleOverride ||
    (currentUser?.activeRole as RoleCode) ||
    ROLES.ADMIN;

  const roleMeta = ROLE_CONFIG[activeRole] || ROLE_CONFIG.ADMIN;

  const displayUser = {
    fullName: currentUser?.fullName || roleMeta.defaultUser.fullName,
    initials: currentUser?.initials || roleMeta.defaultUser.initials,
    roleTitle: roleMeta.roleTitle,
  };

  const getDefaultRouteForRole = (role: RoleCode): string => {
    switch (role) {
      case ROLES.ADMIN:
        return "/admin/users";
      case ROLES.OWNER:
      case ROLES.SALES:
        return "/orders/sales";
      case ROLES.PLANNER:
        return "/bom";
      case ROLES.PURCHASING:
        return "/purchasing/orders";
      case ROLES.WAREHOUSE:
        return "/inventory/stock";
      case ROLES.LEADER:
      case ROLES.QC:
        return "/production/kanban";
      default:
        return "/dashboard";
    }
  };

  const handleRoleSwitch = (targetRole: RoleCode) => {
    switchActiveRole(targetRole);
    navigate(getDefaultRouteForRole(targetRole));
  };

  const handleLogout = async () => {
    try {
      await logoutMutation.mutateAsync();
    } catch {
      // Fallback chuyển trang nếu API lỗi
    } finally {
      navigate("/login");
    }
  };

  const avatarBgClass =
    activeRole === ROLES.ADMIN
      ? "bg-purple-700"
      : activeRole === ROLES.SALES
      ? "bg-blue-600"
      : activeRole === ROLES.OWNER
      ? "bg-amber-600"
      : activeRole === ROLES.PURCHASING
      ? "bg-emerald-600"
      : activeRole === ROLES.WAREHOUSE
      ? "bg-amber-700"
      : activeRole === ROLES.LEADER
      ? "bg-orange-700"
      : activeRole === ROLES.QC
      ? "bg-rose-600"
      : "bg-indigo-600";

  return (
    <header
      className={`h-16 bg-white border-b border-slate-200 px-4 sm:px-6 flex items-center justify-between shrink-0 shadow-2xs z-10 ${className}`}
    >
      {/* Khối bên trái: Nút Hamburger Mobile (A11Y-03, A11Y-04) + Khối điều hướng ngữ cảnh customTopLeft */}
      <div className="flex items-center gap-3">
        {onOpenMobileMenu && (
          <button
            type="button"
            onClick={onOpenMobileMenu}
            aria-label="Mở menu điều hướng"
            className="lg:hidden p-2 rounded-lg text-slate-600 hover:bg-slate-100 border border-slate-200 transition cursor-pointer"
          >
            <svg
              className="w-5 h-5"
              fill="none"
              stroke="currentColor"
              strokeWidth={2}
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M4 6h16M4 12h16M4 18h16"
              />
            </svg>
          </button>
        )}
        {customTopLeft}
      </div>

      {/* Khối bên phải: Multi-Role Switcher (STRUCT-02) + Hồ sơ người dùng + Đăng xuất */}
      <div className="flex items-center gap-3 sm:gap-4">
        {/* Multi-Role Switcher: Chỉ hiển thị khi tài khoản có nhiều hơn 1 vai trò */}
        {currentUser?.roles && currentUser.roles.length > 1 && (
          <div className="flex items-center gap-1 bg-slate-100 p-1 rounded-lg border border-slate-200 text-xs">
            <span className="text-xs font-semibold text-slate-500 px-1.5 hidden md:inline">
              Vai trò:
            </span>
            {currentUser.roles.map((r) => {
              const isCurrent = r === activeRole;
              const cfg = ROLE_CONFIG[r as RoleCode];
              return (
                <button
                  key={r}
                  type="button"
                  onClick={() => handleRoleSwitch(r as RoleCode)}
                  title={`Chuyển sang vai trò ${cfg?.displayName || r}`}
                  className={`px-2.5 py-1 rounded-md text-xs font-semibold transition cursor-pointer ${
                    isCurrent
                      ? "bg-white text-blue-700 shadow-xs border border-slate-200/80"
                      : "text-slate-600 hover:text-slate-900 hover:bg-slate-200/60"
                  }`}
                >
                  {cfg?.badgeLabel || r}
                </button>
              );
            })}
          </div>
        )}

        {/* User Profile Info */}
        <div className="flex items-center gap-2.5">
          <div
            className={`w-9 h-9 rounded-full ${avatarBgClass} text-white text-xs font-bold flex items-center justify-center shadow-xs select-none shrink-0`}
          >
            {displayUser.initials}
          </div>
          <div className="leading-tight hidden sm:block">
            <div className="text-sm font-bold text-slate-800">
              {displayUser.fullName}
            </div>
            <div className="text-xs text-slate-500">{roleMeta.roleTitle}</div>
          </div>
        </div>

        {/* Nút Đăng xuất */}
        <button
          type="button"
          onClick={handleLogout}
          className="px-3.5 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition shadow-2xs cursor-pointer inline-flex items-center gap-1.5"
          title="Đăng xuất khỏi hệ thống"
        >
          <svg
            className="w-4 h-4 text-slate-500"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"
            />
          </svg>
          <span className="hidden sm:inline">Đăng xuất</span>
        </button>
      </div>
    </header>
  );
}
