import type { ReactNode } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useWmms } from '../../features/auth/hooks/WmmsContext';
import { useSession } from '../../features/auth/store/SessionContext';
import { useLogout } from '../../features/auth/hooks/useLogout';
import { ROLE_CONFIG } from '../../constants/wmmsConstants';
import { ROLES, type RoleCode } from '../../constants/roles';

interface SidebarItem {
  icon: string;
  label: string;
  path: string;
  badge?: string;
}

interface SidebarSection {
  heading: string;
  items: SidebarItem[];
}

export const getDefaultRouteForRole = (role: RoleCode): string => {
  switch (role) {
    case ROLES.ADMIN:
      return '/admin/users';
    case ROLES.OWNER:
      return '/orders';
    case ROLES.SALES:
      return '/orders';
    case ROLES.PLANNER:
      return '/bom';
    case ROLES.PURCHASING:
      return '/purchasing/orders';
    case ROLES.WAREHOUSE:
      return '/inventory/stock';
    case ROLES.LEADER:
      return '/production/kanban';
    case ROLES.QC:
      return '/qc/inspection';
    default:
      return '/orders';
  }
};

/**
 * Component Bố cục Điều hướng Toàn cục (Section III.2: Navigation - Global Selector & Top Bar)
 * Chuẩn hóa 100% theo sơ đồ Accessibility trong Report 3 (SRS Screen Inventory):
 * - Cột bên trái (Dark Navy Sidebar `#0d1528`): Logo WMMSWOOD + Active Role Badge, danh mục menu theo thẩm quyền của Role hiện tại.
 * - Thanh đỉnh (Top Bar): Active Space Selector, Multi-Role Switcher (khi user có nhiều vai trò), Avatar Profile, và Logout.
 */
export default function WmmsAppShell({
  children,
  mode,
  customTopLeft = null,
}: {
  children: ReactNode;
  mode?: string;
  customTopLeft?: ReactNode;
}) {
  const navigate = useNavigate();
  const location = useLocation();
  const { orderSpaces, currentSpaceKey, setCurrentSpaceKey } = useWmms();
  const { currentUser, switchActiveRole } = useSession();
  const logoutMutation = useLogout();

  // Xác định Role đang kích hoạt (ưu tiên currentUser.activeRole, fallback mode / ADMIN)
  const activeRole: RoleCode =
    currentUser?.activeRole ||
    (mode === 'sales' || mode === 'staging'
      ? ROLES.SALES
      : mode === 'purchasing'
      ? ROLES.PURCHASING
      : mode === 'owner'
      ? ROLES.OWNER
      : ROLES.ADMIN);

  const roleMeta = ROLE_CONFIG[activeRole] || ROLE_CONFIG.ADMIN;

  const displayUser = {
    fullName: currentUser?.fullName || roleMeta.defaultUser.fullName,
    initials: currentUser?.initials || roleMeta.defaultUser.initials,
    roleTitle: roleMeta.roleTitle,
  };

  // Cấu hình các mục điều hướng Sidebar theo thẩm quyền 8 vai trò chuẩn hóa từ Report 3
  const getSidebarSections = (): SidebarSection[] => {
    switch (activeRole) {
      case ROLES.ADMIN:
        return [
          {
            heading: 'SYSTEM ADMINISTRATION',
            items: [
              { icon: '👥', label: 'User & Multi-Role RBAC', path: '/admin/users' },
              { icon: '🛡️', label: 'System Audit Logs', path: '/admin/audit-logs' },
            ],
          },
          {
            heading: 'ANALYTICS & STOCK',
            items: [
              { icon: '📈', label: 'Production & Quality Reports', path: '/analytics/reports' },
              { icon: '🏬', label: '3-Tier Inventory Stock', path: '/inventory/stock' },
            ],
          },
        ];

      case ROLES.OWNER:
        return [
          {
            heading: 'EXECUTIVE & ORDERS',
            items: [
              { icon: '📊', label: 'Executive Dashboard & ETD', path: '/dashboard' },
              { icon: '📦', label: 'Sales Orders List', path: '/orders' },
              { icon: '🗂️', label: 'Active Order Space', path: `/orders/spaces/${currentSpaceKey}` },
            ],
          },
          {
            heading: 'TECHNICAL & ROUTING',
            items: [
              { icon: '📐', label: 'BOM & Routing Builder', path: '/bom' },
              { icon: '⚙️', label: 'Dynamic Routing Config', path: '/production/routing-config' },
              { icon: '🪵', label: 'Master Material Catalog', path: '/catalog' },
              { icon: '🖼️', label: 'Visual Product Catalog', path: '/catalog/visual' },
            ],
          },
          {
            heading: 'INVENTORY & PURCHASING',
            items: [
              { icon: '⚖️', label: 'Inventory Reconciliation', path: '/inventory/reconciliation' },
              { icon: '🏬', label: '3-Tier Stock Grid', path: '/inventory/stock' },
              { icon: '🛒', label: 'Purchase Orders (PO)', path: '/purchasing/orders' },
              { icon: '📝', label: 'Daily Consolidated PR', path: '/purchasing/requisitions' },
            ],
          },
          {
            heading: 'SHOP FLOOR & AUDIT',
            items: [
              { icon: '📋', label: 'Production Kanban', path: '/production/kanban' },
              { icon: '⚡', label: 'Overdriver Approvals', path: '/production/overdriver' },
              { icon: '🔄', label: 'Replacement Approvals', path: '/production/replacement-approvals' },
              { icon: '📈', label: 'Quality & Analytics Reports', path: '/analytics/reports' },
              { icon: '🛡️', label: 'System Audit Logs', path: '/admin/audit-logs' },
            ],
          },
        ];

      case ROLES.SALES:
        return [
          {
            heading: 'ORDER INTAKE & MANAGEMENT',
            items: [
              { icon: '📦', label: 'Sales Orders List', path: '/orders' },
              { icon: '📄', label: 'PO Excel Staging Grid', path: '/orders/staging' },
              { icon: '🗂️', label: 'Active Order Space', path: `/orders/spaces/${currentSpaceKey}` },
            ],
          },
          {
            heading: 'TECHNICAL & INVENTORY',
            items: [
              { icon: '📐', label: 'BOM & Routing Breakdown', path: '/bom' },
              { icon: '⚖️', label: 'Inventory Reconciliation', path: '/inventory/reconciliation' },
            ],
          },
        ];

      case ROLES.PLANNER:
        return [
          {
            heading: 'MATERIAL PLANNING & BOM',
            items: [
              { icon: '📐', label: 'BOM & Routing Builder', path: '/bom' },
              { icon: '⚖️', label: 'Inventory Reconciliation', path: '/inventory/reconciliation' },
              { icon: '🪵', label: 'Master Materials & SKUs', path: '/catalog' },
              { icon: '🛒', label: 'Purchase Orders (PO)', path: '/purchasing/orders' },
            ],
          },
        ];

      case ROLES.PURCHASING:
        return [
          {
            heading: 'PURCHASING OPERATIONS',
            items: [
              { icon: '🛒', label: 'Purchase Orders (PO)', path: '/purchasing/orders' },
              { icon: '📝', label: 'Daily Consolidated PR', path: '/purchasing/requisitions' },
              { icon: '🪵', label: 'Vendor & Material Catalog', path: '/catalog' },
            ],
          },
          {
            heading: 'WAREHOUSE & STOCK',
            items: [
              { icon: '🏬', label: '3-Tier Inventory Stock', path: '/inventory/stock' },
            ],
          },
        ];

      case ROLES.WAREHOUSE:
        return [
          {
            heading: 'WAREHOUSE OPERATIONS',
            items: [
              { icon: '🏬', label: '3-Tier Stock Grid', path: '/inventory/stock' },
              { icon: '📥', label: 'Goods Receipt & Commit', path: '/warehouse/receipts' },
              { icon: '📤', label: 'Goods Issue & Handover', path: '/warehouse/issues' },
              { icon: '📦', label: 'Finished Goods Receipt', path: '/warehouse/finished-goods' },
            ],
          },
          {
            heading: 'CATALOG & FLOOR',
            items: [
              { icon: '🪵', label: 'Master Material Catalog', path: '/catalog' },
              { icon: '📋', label: 'Production Kanban', path: '/production/kanban' },
            ],
          },
        ];

      case ROLES.LEADER:
        return [
          {
            heading: 'PRODUCTION FLOOR',
            items: [
              { icon: '📊', label: 'Executive Dashboard & ETD', path: '/dashboard' },
              { icon: '📋', label: 'Production Kanban Board', path: '/production/kanban' },
              { icon: '⏱️', label: 'Workstation Stage Progress', path: '/production/progress' },
              { icon: '🗂️', label: 'Active Order Space', path: `/orders/spaces/${currentSpaceKey}` },
            ],
          },
          {
            heading: 'APPROVALS & ANALYTICS',
            items: [
              { icon: '⚡', label: 'Overdriver Requests', path: '/production/overdriver' },
              { icon: '📈', label: 'Production & Quality Reports', path: '/analytics/reports' },
            ],
          },
        ];

      case ROLES.QC:
        return [
          {
            heading: 'QUALITY CONTROL',
            items: [
              { icon: '🔍', label: 'QC Inspection & Defect Log', path: '/qc/inspection' },
              { icon: '📋', label: 'Production Kanban Board', path: '/production/kanban' },
            ],
          },
        ];

      default:
        return [
          {
            heading: 'OPERATIONAL SCREENS',
            items: [{ icon: '📦', label: 'Sales Orders', path: '/orders' }],
          },
        ];
    }
  };

  const isItemActive = (path: string): boolean => {
    if (path.includes('/orders/spaces/')) {
      return location.pathname.startsWith('/orders/spaces');
    }
    return location.pathname === path || (path !== '/' && location.pathname.startsWith(path + '/'));
  };

  const handleLogout = () => {
    logoutMutation.mutate();
  };

  const handleSpaceChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newKey = e.target.value;
    setCurrentSpaceKey(newKey);
    if (location.pathname.startsWith('/orders/spaces')) {
      navigate(`/orders/spaces/${newKey}`);
    }
  };

  const handleRoleSwitch = (targetRole: RoleCode) => {
    if (targetRole === activeRole) return;
    switchActiveRole(targetRole);
    navigate(getDefaultRouteForRole(targetRole));
  };

  const avatarBgClass =
    activeRole === ROLES.ADMIN
      ? 'bg-purple-700'
      : activeRole === ROLES.SALES
      ? 'bg-blue-600'
      : activeRole === ROLES.OWNER
      ? 'bg-amber-600'
      : activeRole === ROLES.PURCHASING
      ? 'bg-emerald-600'
      : activeRole === ROLES.WAREHOUSE
      ? 'bg-amber-700'
      : activeRole === ROLES.LEADER
      ? 'bg-orange-700'
      : activeRole === ROLES.QC
      ? 'bg-rose-600'
      : 'bg-indigo-600';

  return (
    <div className="min-h-screen flex bg-[#f4f6f9] text-slate-800">
      {/* Cột điều hướng bên trái (Global Selector Sidebar) */}
      <aside className="w-60 bg-[#0d1528] text-slate-300 flex flex-col justify-between shrink-0 select-none border-r border-slate-800">
        <div>
          {/* Logo Header + Role Badge */}
          <div className="h-14 px-4 flex items-center justify-between border-b border-slate-800/80">
            <div className="flex items-center gap-2">
              <span className="text-white font-extrabold tracking-tight text-base">
                WMMSWOOD
              </span>
              <span
                className={`text-[10px] font-bold px-1.5 py-0.5 rounded uppercase tracking-wider ${roleMeta.badgeClass}`}
              >
                {roleMeta.badgeLabel}
              </span>
            </div>
          </div>

          {/* Danh sách các nhóm menu điều hướng theo vai trò */}
          <nav className="py-3 px-2 space-y-4 overflow-y-auto max-h-[calc(100vh-7.5rem)]">
            {getSidebarSections().map((section, idx) => (
              <div key={idx}>
                <div className="px-2.5 mb-1.5 text-[10px] font-bold uppercase tracking-wider text-slate-500">
                  {section.heading}
                </div>
                <ul className="space-y-0.5">
                  {section.items.map((item, i) => {
                    const active = isItemActive(item.path);
                    return (
                      <li key={i}>
                        <button
                          type="button"
                          onClick={() => navigate(item.path)}
                          className={`w-full flex items-center gap-2.5 px-2.5 py-2 rounded-md text-xs font-medium transition-colors ${
                            active
                              ? 'bg-[#172544] text-blue-400 font-semibold border-l-2 border-blue-500'
                              : 'text-slate-300 hover:bg-slate-800/60 hover:text-white'
                          }`}
                        >
                          <span className="text-sm leading-none">{item.icon}</span>
                          <span className="truncate">{item.label}</span>
                        </button>
                      </li>
                    );
                  })}
                </ul>
              </div>
            ))}
          </nav>
        </div>

        {/* Chân trang Sidebar */}
        <div className="px-4 py-3 border-t border-slate-800/80 text-[11px] text-slate-400 space-y-0.5">
          <div className="font-medium text-slate-400">WMMS System v2.6.4</div>
          <div className="text-slate-500 truncate" title={displayUser.roleTitle}>
            Role: {displayUser.roleTitle}
          </div>
        </div>
      </aside>

      {/* Khu vực nội dung chính bên phải */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Thanh Header trên cùng */}
        <header className="h-14 bg-white border-b border-slate-200 px-6 flex items-center justify-between shrink-0">
          {/* Góc trái: Dropdown Current Space / Active Order Space */}
          <div className="flex items-center gap-3">
            {customTopLeft ? (
              customTopLeft
            ) : (
              <div className="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-md px-3 py-1.5 text-xs">
                <span className="text-slate-500 font-medium">
                  {location.pathname.startsWith('/bom')
                    ? 'Target Order Space:'
                    : activeRole === ROLES.OWNER
                    ? 'Active Order Space:'
                    : 'Current Space:'}
                </span>
                <select
                  value={currentSpaceKey}
                  onChange={handleSpaceChange}
                  className="bg-transparent font-semibold text-slate-800 focus:outline-none cursor-pointer pr-1"
                >
                  {orderSpaces.map((sp) => (
                    <option key={sp.spaceKey} value={sp.spaceKey}>
                      {sp.spaceLabel || `${sp.spaceKey} (${sp.customerName})`}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {/* Góc phải: Multi-Role Switcher + Thông tin nhân sự + Logout */}
          <div className="flex items-center gap-3 sm:gap-4">
            {/* Multi-Role Switcher (Chỉ hiển thị khi tài khoản có >= 2 vai trò) */}
            {currentUser?.roles && currentUser.roles.length > 1 && (
              <div className="flex items-center gap-1 bg-slate-100 p-1 rounded-lg border border-slate-200 text-xs">
                <span className="text-[11px] font-semibold text-slate-500 px-1 hidden md:inline">
                  Vai trò:
                </span>
                {currentUser.roles.map((r) => {
                  const isCurrent = r === activeRole;
                  const cfg = ROLE_CONFIG[r];
                  return (
                    <button
                      key={r}
                      type="button"
                      onClick={() => handleRoleSwitch(r)}
                      title={`Chuyển sang vai trò ${cfg?.displayName || r}`}
                      className={`px-2 py-1 rounded text-xs font-semibold transition ${
                        isCurrent
                          ? 'bg-white text-slate-900 shadow-sm border border-slate-200/80'
                          : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/60'
                      }`}
                    >
                      {cfg?.badgeLabel || r}
                    </button>
                  );
                })}
              </div>
            )}

            {/* Thông tin nhân sự đang đăng nhập */}
            <div className="flex items-center gap-2.5">
              <div
                className={`w-8 h-8 rounded-full ${avatarBgClass} text-white text-xs font-bold flex items-center justify-center shadow-sm`}
              >
                {displayUser.initials}
              </div>
              <div className="leading-tight text-right sm:text-left">
                <div className="text-xs font-bold text-slate-800">
                  {displayUser.fullName}
                </div>
                <div className="text-[11px] text-slate-500">{roleMeta.roleTitle}</div>
              </div>
            </div>

            {/* Nút Logout */}
            <button
              type="button"
              onClick={handleLogout}
              className="px-3 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition shadow-sm"
            >
              Logout
            </button>
          </div>
        </header>

        {/* Nội dung trang */}
        <main className="flex-1 p-6 overflow-y-auto">{children}</main>
      </div>
    </div>
  );
}
