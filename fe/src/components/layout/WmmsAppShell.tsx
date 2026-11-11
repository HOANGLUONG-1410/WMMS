import { useState, type ReactNode } from "react";
import { useSession } from "../../features/auth/store/SessionContext";
import { ROLES, type RoleCode } from "../../constants/roles";
import WmmsHeader from "./WmmsHeader";
import WmmsSidebar from "./WmmsSidebar";

export interface WmmsAppShellProps {
  children: ReactNode;
  mode?: string;
  customTopLeft?: ReactNode;
}

export const getDefaultRouteForRole = (role: RoleCode): string => {
  switch (role) {
    case ROLES.ADMIN:
      return "/admin/users";
    case ROLES.OWNER:
    case ROLES.SALES:
      return "/orders";
    case ROLES.PLANNER:
      return "/bom";
    case ROLES.PURCHASING:
      return "/purchasing/orders";
    case ROLES.WAREHOUSE:
      return "/inventory/stock";
    case ROLES.LEADER:
      return "/production/kanban";
    case ROLES.QC:
      return "/qc/inspection";
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
          customTopLeft={customTopLeft}
          onOpenMobileMenu={() => setMobileMenuOpen(true)}
          activeRoleOverride={activeRole}
        />

        {/* Page Content */}
        <main className="flex-1 p-6 md:p-8 overflow-y-auto bg-[#F8FAFC]">
          {children}
        </main>
      </div>
    </div>
  );
}
