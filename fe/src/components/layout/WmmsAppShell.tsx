import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { Outlet } from "react-router-dom";
import { useSession } from "../../features/auth/store/SessionContext";
import { ROLES, type RoleCode } from "../../constants/roles";
import WmmsHeader from "./WmmsHeader";
import WmmsSidebar from "./WmmsSidebar";

export interface WmmsAppShellProps {
  children?: ReactNode;
  mode?: string;
  customTopLeft?: ReactNode;
}

interface HeaderSlotContextValue {
  setCustomTopLeft: (node: ReactNode | null) => void;
}

const HeaderSlotContext = createContext<HeaderSlotContextValue | null>(null);

/**
 * Đặt nội dung tùy biến bên trái Header (VD: nút quay lại danh sách đơn hàng, badge Space)
 * từ bên trong các trang con khi WmmsAppShell đóng vai trò Persistent Layout Route.
 */
export function HeaderTopLeft({ children }: { children: ReactNode }) {
  const ctx = useContext(HeaderSlotContext);

  useEffect(() => {
    if (!ctx) return;
    ctx.setCustomTopLeft(children);
  }, [ctx, children]);

  useEffect(() => {
    if (!ctx) return;
    return () => ctx.setCustomTopLeft(null);
  }, [ctx]);

  return null;
}

export const getDefaultRouteForRole = (role: RoleCode): string => {
  switch (role) {
    case ROLES.ADMIN:
      return "/admin/users";
    case ROLES.OWNER:
    case ROLES.SALES:
    case ROLES.PLANNER:
    case ROLES.LEADER:
    case ROLES.QC:
      return "/orders";
    case ROLES.PURCHASING:
      return "/purchasing/orders";
    case ROLES.WAREHOUSE:
      return "/inventory/stock";
    default:
      return "/orders";
  }
};

export default function WmmsAppShell({
  children,
  mode,
  customTopLeft = null,
}: WmmsAppShellProps) {
  const { currentUser } = useSession();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [slotTopLeft, setSlotTopLeft] = useState<ReactNode | null>(null);

  const slotCtx = useMemo(
    () => ({ setCustomTopLeft: setSlotTopLeft }),
    [],
  );

  const activeRole: RoleCode =
    (currentUser?.activeRole as RoleCode) ||
    (mode === "sales" || mode === "staging"
      ? ROLES.SALES
      : mode === "purchasing"
      ? ROLES.PURCHASING
      : mode === "owner"
      ? ROLES.OWNER
      : ROLES.ADMIN);

  return (
    <HeaderSlotContext.Provider value={slotCtx}>
      <div className="min-h-screen flex bg-[#F8FAFC] text-[#1E293B]">
        {/* Sidebar Component (STRUCT-02: Reusable separate navigation component) */}
        <WmmsSidebar
          mobileOpen={mobileMenuOpen}
          onCloseMobile={() => setMobileMenuOpen(false)}
          activeRoleOverride={activeRole}
        />

        {/* Main Content Area */}
        <div className="flex-1 flex flex-col min-w-0 bg-[#F8FAFC]">
          {/* Header Component (STRUCT-02: Reusable separate header component) */}
          <WmmsHeader
            customTopLeft={customTopLeft ?? slotTopLeft}
            onOpenMobileMenu={() => setMobileMenuOpen(true)}
            activeRoleOverride={activeRole}
          />

          {/* Page Content */}
          <main className="flex-1 p-6 md:p-8 overflow-y-auto bg-[#F8FAFC]">
            {children ?? <Outlet />}
          </main>
        </div>
      </div>
    </HeaderSlotContext.Provider>
  );
}
