import type { ReactNode } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useWmms } from '../../features/auth/hooks/WmmsContext';
import { useSession } from '../../features/auth/store/SessionContext';
import { useLogout } from '../../features/auth/hooks/useLogout';
import { ROLE_CONFIG } from '../../constants/wmmsConstants';

/**
 * Component Bố cục Điều hướng Toàn cục (Section III.2: Navigation - Global selector & Top Bar)
 * Hiển thị đúng theo thiết kế nguyên bản trong Report 3:
 * - Cột bên trái (Dark Navy Sidebar `#0b132b`): Logo WMMS / WMMSWOOD + Role Badge, các nhóm điều hướng theo vai trò, và chân trang `WMMS System v2.6.4`.
 * - Thanh đỉnh (Top Bar): Dropdown chuyển đổi `Current Space` (hoặc nhãn ngữ cảnh), Role Switcher nhanh, Avatar viết tắt + Họ tên + Vai trò, và nút `Logout`.
 */
export default function WmmsAppShell({
  children,
  mode = 'admin', // 'admin' | 'sales' | 'staging' | 'owner' | 'purchasing'
  customTopLeft = null
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

  // Xác định cấu hình hiển thị thương hiệu và hồ sơ theo màn hình hiện tại
  const activeRoleKey =
    mode === 'admin'
      ? 'ADMIN'
      : mode === 'sales' || mode === 'staging'
      ? 'SALES'
      : mode === 'purchasing'
      ? 'PURCHASING'
      : 'OWNER';

  const roleMeta =
    ROLE_CONFIG[activeRoleKey] ||
    (currentUser?.activeRole && ROLE_CONFIG[currentUser.activeRole]) ||
    ROLE_CONFIG.ADMIN;

  const displayUser = {
    fullName: currentUser?.fullName || roleMeta.defaultUser.fullName,
    initials: currentUser?.initials || roleMeta.defaultUser.initials,
    roleTitle: roleMeta.roleTitle
  };

  // Cấu hình menu bên trái theo đúng ảnh chụp màn hình trong Section III.2, III.3, III.4
  const getSidebarSections = () => {
    if (mode === 'purchasing') {
      // Section III.2.1: Global selector (WMMS PURCHASING)
      return [
        {
          heading: 'NAVIGATION PORTAL',
          items: [{ icon: '🏠', label: 'All Prototypes Portal', path: '/admin/users' }]
        },
        {
          heading: 'PURCHASING OPERATIONS',
          items: [
            { icon: '🛒', label: 'Purchase Orders (PO)', path: '/purchasing/orders', active: location.pathname.startsWith('/purchasing') },
            { icon: '📝', label: 'Daily Requisitions (PR)', path: '/orders' },
            { icon: '🪵', label: 'Vendor & Material Catalog', path: '/catalog', active: location.pathname === '/catalog' }
          ]
        },
        {
          heading: 'WAREHOUSE & STOCK',
          items: [
            { icon: '🏬', label: '3-Tier Inventory', path: '/orders' },
            { icon: '📥', label: 'Goods Inbound Schedule', path: '/orders' }
          ]
        }
      ];
    }

    if (mode === 'sales' || mode === 'staging') {
      // Section III.4.1 & III.4.2: Sales Orders Directory & Safe Excel PO Staging Grid
      return [
        {
          heading: 'NAVIGATION PORTAL',
          items: [{ icon: '🏠', label: 'All Prototypes Portal', path: '/admin/users' }]
        },
        {
          heading: 'ORDER MANAGEMENT',
          items: [
            {
              icon: '📦',
              label: 'Sales Orders List',
              path: '/orders',
              active: location.pathname === '/orders'
            },
            {
              icon: '📄',
              label: 'PO Excel Staging',
              path: '/orders/staging',
              active: location.pathname === '/orders/staging'
            },
            {
              icon: '🗂️',
              label: 'Active Order Space',
              path: `/orders/spaces/${currentSpaceKey}`,
              active: location.pathname.startsWith('/orders/spaces')
            }
          ]
        },
        {
          heading: 'PRODUCTION & FLOOR',
          items: [
            { icon: '📊', label: 'Executive Dashboard', path: `/orders/spaces/${currentSpaceKey}` },
            { icon: '📋', label: 'Production Kanban', path: `/orders/spaces/${currentSpaceKey}` }
          ]
        }
      ];
    }

    if (mode === 'owner') {
      // Section III.4.3: Order Space Detail Dashboard (WMMSWOOD OWNER)
      return [
        {
          heading: 'NAVIGATION PORTAL',
          items: [{ icon: '🏠', label: 'All Prototypes Portal', path: '/admin/users' }]
        },
        {
          heading: 'ACTIVE SPACE DETAILS',
          items: [
            {
              icon: '🗂️',
              label: 'Space Overview',
              path: `/orders/spaces/${currentSpaceKey}`,
              active: location.pathname.startsWith('/orders/spaces')
            },
            { icon: '📐', label: 'Breakdown BOM & Routing', path: '/bom', active: location.pathname.startsWith('/bom') },
            { icon: '⚖️', label: 'Inventory Reconciliation', path: '/orders' },
            { icon: '📋', label: 'Production Kanban', path: '/orders' }
          ]
        },
        {
          heading: 'EXECUTIVE & FLOOR',
          items: [
            { icon: '📊', label: 'Executive Dashboard', path: '/orders' },
            { icon: '🪵', label: 'Product & Material Catalog', path: '/catalog', active: location.pathname === '/catalog' },
            { icon: '📝', label: 'Daily Consolidated PR', path: '/orders' },
            { icon: '🏬', label: '3-Tier Stock', path: '/orders' }
          ]
        }
      ];
    }

    // Mặc định Section III.3: System Administration (SCR-ADM-01 & SCR-ADM-02)
    return [
      {
        heading: 'NAVIGATION PORTAL',
        items: [{ icon: '🏠', label: 'All Prototypes Portal', path: '/admin/users' }]
      },
      {
        heading: 'EXECUTIVE & ORDERS',
        items: [
          { icon: '📊', label: 'Executive Dashboard', path: `/orders/spaces/${currentSpaceKey}` },
          {
            icon: '📦',
            label: 'Sales Orders List',
            path: '/orders',
            active: location.pathname === '/orders'
          },
          {
            icon: '🗂️',
            label: 'Active Order Space',
            path: `/orders/spaces/${currentSpaceKey}`,
            active: location.pathname.startsWith('/orders/spaces')
          }
        ]
      },
      {
        heading: 'TECHNICAL & STOCK',
        items: [
          { icon: '📐', label: 'BOM & Routing Builder', path: '/bom', active: location.pathname.startsWith('/bom') },
          { icon: '🪵', label: 'Master Materials & SKUs', path: '/catalog', active: location.pathname === '/catalog' },
          { icon: '🏬', label: '3-Tier Inventory', path: '/orders' },
          { icon: '🛒', label: 'Purchasing (PO)', path: '/purchasing/orders', active: location.pathname.startsWith('/purchasing') }
        ]
      },
      {
        heading: 'SHOP FLOOR & QUALITY',
        items: [
          { icon: '📋', label: 'Production Kanban', path: '/orders' },
          { icon: '🔍', label: 'QC Inspection Queue', path: '/orders' },
          { icon: '📈', label: 'Analytics & Excel', path: '/admin/audit-logs' }
        ]
      },
      {
        heading: 'SYSTEM ADMINISTRATION',
        items: [
          {
            icon: '👥',
            label: 'Users & Permissions',
            path: '/admin/users',
            active: location.pathname === '/admin/users'
          },
          {
            icon: '🛡️',
            label: 'System Audit Logs',
            path: '/admin/audit-logs',
            active: location.pathname === '/admin/audit-logs'
          }
        ]
      }
    ];
  };

  const handleLogout = () => {
    logoutMutation.mutate();
  };

  const handleSpaceChange = (e) => {
    const newKey = e.target.value;
    setCurrentSpaceKey(newKey);
    if (location.pathname.startsWith('/orders/spaces')) {
      navigate(`/orders/spaces/${newKey}`);
    }
  };

  const avatarBgClass =
    activeRoleKey === 'ADMIN'
      ? 'bg-slate-700'
      : activeRoleKey === 'SALES'
      ? 'bg-blue-600'
      : activeRoleKey === 'OWNER'
      ? 'bg-amber-600'
      : 'bg-emerald-600';

  return (
    <div className="min-h-screen flex bg-[#f4f6f9] text-slate-800">
      {/* Cột điều hướng bên trái (Global Selector Sidebar - Section III.2.1) */}
      <aside className="w-60 bg-[#0d1528] text-slate-300 flex flex-col justify-between shrink-0 select-none border-r border-slate-800">
        <div>
          {/* Logo Header + Role Badge */}
          <div className="h-14 px-4 flex items-center gap-2 border-b border-slate-800/80">
            <span className="text-white font-extrabold tracking-tight text-base">
              {mode === 'purchasing' ? 'WMMS' : 'WMMSWOOD'}
            </span>
            <span
              className={`text-[10px] font-bold px-1.5 py-0.5 rounded uppercase tracking-wider ${roleMeta.badgeClass}`}
            >
              {roleMeta.badgeLabel}
            </span>
          </div>

          {/* Danh sách các nhóm menu điều hướng */}
          <nav className="py-3 px-2 space-y-4 overflow-y-auto max-h-[calc(100vh-7.5rem)]">
            {getSidebarSections().map((section, idx) => (
              <div key={idx}>
                <div className="px-2.5 mb-1.5 text-[10px] font-bold uppercase tracking-wider text-slate-500">
                  {section.heading}
                </div>
                <ul className="space-y-0.5">
                  {section.items.map((item, i) => (
                    <li key={i}>
                      <button
                        type="button"
                        onClick={() => navigate(item.path)}
                        className={`w-full flex items-center gap-2.5 px-2.5 py-2 rounded-md text-xs font-medium transition-colors ${
                          item.active
                            ? 'bg-[#172544] text-blue-400 font-semibold border-l-2 border-blue-500'
                            : 'text-slate-300 hover:bg-slate-800/60 hover:text-white'
                        }`}
                      >
                        <span className="text-sm leading-none">{item.icon}</span>
                        <span className="truncate">{item.label}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>
        </div>

        {/* Chân trang Sidebar (Footer: WMMS System v2.6.4 + Role) */}
        <div className="px-4 py-3 border-t border-slate-800/80 text-[11px] text-slate-400 space-y-0.5">
          <div className="font-medium text-slate-400">WMMS System v2.6.4</div>
          <div className="text-slate-500">Role: {displayUser.roleTitle}</div>
        </div>
      </aside>

      {/* Khu vực nội dung chính bên phải */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Thanh Header trên cùng (Current Space Switcher + User Profile + Logout) */}
        <header className="h-14 bg-white border-b border-slate-200 px-6 flex items-center justify-between shrink-0">
          {/* Góc trái: Dropdown Current Space / Active Order Space hoặc Nhãn Sandbox */}
          <div className="flex items-center gap-3">
            {customTopLeft ? (
              customTopLeft
            ) : (
              <div className="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-md px-3 py-1.5 text-xs">
                <span className="text-slate-500 font-medium">
                  {location.pathname.startsWith('/bom')
                    ? 'Target Order Space:'
                    : mode === 'owner'
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

          {/* Góc phải: Bộ chuyển nhanh màn hình theo Role + Hồ sơ nhân sự + Nút Logout */}
          <div className="flex items-center gap-4">
            {/* Thanh chuyển đổi nhanh giữa các màn hình đặc tả trong Report 3 */}
            <div className="hidden xl:flex items-center gap-1 bg-slate-100 p-1 rounded-md text-[11px]">
              <button
                type="button"
                onClick={() => {
                  switchActiveRole('ADMIN');
                  navigate('/admin/users');
                }}
                className={`px-2 py-1 rounded font-medium transition ${
                  location.pathname.startsWith('/admin')
                    ? 'bg-white text-slate-900 shadow-sm'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                III.3 Admin & RBAC
              </button>
              <button
                type="button"
                onClick={() => {
                  switchActiveRole('SALES');
                  navigate('/orders');
                }}
                className={`px-2 py-1 rounded font-medium transition ${
                  location.pathname === '/orders' || location.pathname === '/orders/staging'
                    ? 'bg-white text-blue-600 shadow-sm'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                III.4 Order Intake (Sales)
              </button>
              <button
                type="button"
                onClick={() => {
                  switchActiveRole('OWNER');
                  navigate(`/orders/spaces/${currentSpaceKey}`);
                }}
                className={`px-2 py-1 rounded font-medium transition ${
                  location.pathname.startsWith('/orders/spaces')
                    ? 'bg-white text-amber-700 shadow-sm'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                III.4.3 Space Dashboard
              </button>
            </div>

            {/* Thông tin nhân sự đang đăng nhập */}
            <div className="flex items-center gap-2.5">
              <div
                className={`w-8 h-8 rounded-full ${avatarBgClass} text-white text-xs font-bold flex items-center justify-center`}
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

            {/* Nút Logout (UC-02) */}
            <button
              type="button"
              onClick={handleLogout}
              className="px-3 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition"
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
