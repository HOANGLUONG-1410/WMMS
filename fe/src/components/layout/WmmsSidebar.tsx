import { useEffect, useRef, useState, type ReactNode } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useWmms } from "../../features/auth/hooks/WmmsContext";
import { useSession } from "../../features/auth/store/SessionContext";
import { ROLE_CONFIG } from "../../constants/wmmsConstants";
import { ROLES, type RoleCode } from "../../constants/roles";

export interface WmmsSidebarProps {
  /**
   * Trạng thái mở menu trên thiết bị di động (Drawer)
   */
  mobileOpen: boolean;

  /**
   * Callback đóng menu trên thiết bị di động
   */
  onCloseMobile: () => void;

  /**
   * Vai trò ghi đè tùy chọn (nếu có)
   */
  activeRoleOverride?: RoleCode;
}

interface SidebarItem {
  icon: ReactNode;
  label: string;
  path: string;
  badge?: string;
  children?: SidebarItem[];
}

interface SidebarSection {
  heading: string;
  items: SidebarItem[];
}

const Icons = {
  users: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
    </svg>
  ),
  audit: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
    </svg>
  ),
  reports: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
    </svg>
  ),
  inventory: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
    </svg>
  ),
  dashboard: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
    </svg>
  ),
  orders: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z" />
    </svg>
  ),
  space: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
    </svg>
  ),
  bom: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
    </svg>
  ),
  routing: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
    </svg>
  ),
  catalog: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4" />
    </svg>
  ),
  reconcile: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3" />
    </svg>
  ),
  purchasing: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z" />
    </svg>
  ),
  kanban: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
    </svg>
  ),
  bolt: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
    </svg>
  ),
  qc: (
    <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
    </svg>
  ),
  subItem: (
    <svg className="w-3.5 h-3.5 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" d="M5 4v8a4 4 0 004 4h10m0 0l-4-4m4 4l-4 4" />
    </svg>
  ),
};

const SIDEBAR_STORAGE_KEY = "wmms_sidebar_width";
const DEFAULT_SIDEBAR_WIDTH = 280;
const MIN_SIDEBAR_WIDTH = 220;
const MAX_SIDEBAR_WIDTH = 460;

export default function WmmsSidebar({
  mobileOpen,
  onCloseMobile,
  activeRoleOverride,
}: WmmsSidebarProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const { currentSpaceKey } = useWmms();
  const { currentUser } = useSession();

  // Độ rộng có thể kéo thả của Sidebar trên màn hình Desktop
  const [sidebarWidth, setSidebarWidth] = useState<number>(() => {
    try {
      const saved = localStorage.getItem(SIDEBAR_STORAGE_KEY);
      if (saved) {
        const parsed = parseInt(saved, 10);
        if (!isNaN(parsed) && parsed >= MIN_SIDEBAR_WIDTH && parsed <= MAX_SIDEBAR_WIDTH) {
          return parsed;
        }
      }
    } catch {
      // Fallback nếu localStorage bị chặn
    }
    return DEFAULT_SIDEBAR_WIDTH;
  });

  const [isResizing, setIsResizing] = useState<boolean>(false);
  const isResizingRef = useRef<boolean>(false);

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isResizingRef.current) return;
      const nextWidth = Math.max(MIN_SIDEBAR_WIDTH, Math.min(MAX_SIDEBAR_WIDTH, e.clientX));
      setSidebarWidth(nextWidth);
    };

    const handleMouseUp = () => {
      if (!isResizingRef.current) return;
      isResizingRef.current = false;
      setIsResizing(false);
      document.body.style.removeProperty("cursor");
      document.body.style.removeProperty("user-select");
      setSidebarWidth((latest) => {
        try {
          localStorage.setItem(SIDEBAR_STORAGE_KEY, String(latest));
        } catch {
          // ignore
        }
        return latest;
      });
    };

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);

    return () => {
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
      document.body.style.removeProperty("cursor");
      document.body.style.removeProperty("user-select");
    };
  }, []);

  const handleStartResize = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    isResizingRef.current = true;
    setIsResizing(true);
    document.body.style.cursor = "col-resize";
    document.body.style.userSelect = "none";
  };

  const handleResetWidth = () => {
    setSidebarWidth(DEFAULT_SIDEBAR_WIDTH);
    try {
      localStorage.setItem(SIDEBAR_STORAGE_KEY, String(DEFAULT_SIDEBAR_WIDTH));
    } catch {
      // ignore
    }
  };

  const activeRole: RoleCode =
    activeRoleOverride ||
    (currentUser?.activeRole as RoleCode) ||
    ROLES.ADMIN;

  const roleMeta = ROLE_CONFIG[activeRole] || ROLE_CONFIG.ADMIN;

  const isInOrderSpace = location.pathname.startsWith("/orders/spaces");
  const isInBomPage = location.pathname.startsWith("/bom");
  const isInReconciliation = location.pathname.startsWith("/inventory/reconciliation");
  const isInProductionPage =
    location.pathname.startsWith("/production/progress") ||
    location.pathname.startsWith("/production/kanban");

  // Lấy spaceKey từ URL: hoặc /orders/spaces/:spaceKey hoặc /bom?spaceKey=... hoặc /inventory/reconciliation?spaceKey=... hoặc /production/progress?spaceKey=...
  const spaceFromPath = location.pathname.startsWith("/orders/spaces/")
    ? decodeURIComponent(location.pathname.replace("/orders/spaces/", "").split("/")[0] || "")
    : "";
  const spaceFromQuery = new URLSearchParams(location.search).get("spaceKey") || "";
  const orderItemIdFromQuery = new URLSearchParams(location.search).get("orderItemId") || "";
  const skuFromQuery = new URLSearchParams(location.search).get("sku") || "";
  const activeSpaceFromUrl = spaceFromPath || spaceFromQuery;
  const displaySpaceKey = activeSpaceFromUrl || currentSpaceKey;

  // Hiển thị cây phân cấp khi đang ở trong Space hoặc đang ở trang BOM / Đối chiếu / Tiến trình của Space
  const isSpaceContextActive =
    isInOrderSpace ||
    ((isInBomPage || isInReconciliation || isInProductionPage) && Boolean(displaySpaceKey));

  const spaceSubItems: SidebarItem[] = [];
  if (isSpaceContextActive && displaySpaceKey) {
    spaceSubItems.push({
      icon: Icons.bom,
      label: "BOM & quy trình sản xuất",
      path: `/bom?spaceKey=${encodeURIComponent(displaySpaceKey)}`,
    });
    spaceSubItems.push({
      icon: Icons.reconcile,
      label: "Đối chiếu & phân bổ vật tư",
      path: `/inventory/reconciliation?spaceKey=${encodeURIComponent(displaySpaceKey)}`,
    });

    // Tab Sản phẩm và Tab con Tiến trình sản xuất: CHỈ hiện khi người dùng truy cập trang tiến trình sản xuất của sản phẩm
    if (isInProductionPage && Boolean(orderItemIdFromQuery)) {
      const productTitle = skuFromQuery ? `Sản phẩm ${skuFromQuery}` : `Sản phẩm #${orderItemIdFromQuery}`;
      const prodPath = `/production/progress?spaceKey=${encodeURIComponent(displaySpaceKey)}&orderItemId=${encodeURIComponent(orderItemIdFromQuery)}${skuFromQuery ? `&sku=${encodeURIComponent(skuFromQuery)}` : ""}`;
      spaceSubItems.push({
        icon: Icons.catalog,
        label: productTitle,
        path: prodPath,
        children: [
          {
            icon: Icons.routing,
            label: "Tiến trình sản xuất",
            path: prodPath,
          },
        ],
      });
    }
  }

  const orderSpaceChildItem: SidebarItem | undefined = isSpaceContextActive
    ? {
        icon: Icons.space,
        label: displaySpaceKey || "Không gian đơn hàng",
        path: `/orders/spaces/${displaySpaceKey}`,
        children: spaceSubItems.length > 0 ? spaceSubItems : undefined,
      }
    : undefined;

  const getSidebarSections = (): SidebarSection[] => {
    switch (activeRole) {
      case ROLES.ADMIN:
        return [
          {
            heading: "QUẢN TRỊ HỆ THỐNG",
            items: [
              { icon: Icons.users, label: "Tài khoản & Phân quyền", path: "/admin/users" },
              { icon: Icons.audit, label: "Nhật ký hoạt động", path: "/admin/audit-logs" },
            ],
          },
          {
            heading: "BÁO CÁO & TỒN KHO",
            items: [
              { icon: Icons.reports, label: "Báo cáo sản xuất & KCS", path: "/analytics/reports" },
              { icon: Icons.inventory, label: "Tồn kho vật tư 3 cấp", path: "/inventory/stock" },
              { icon: Icons.audit, label: "Yêu cầu mua hàng (PR)", path: "/purchasing/requisitions" },
              { icon: Icons.orders, label: "Nhập kho vật tư", path: "/warehouse/receipts" },
              { icon: Icons.space, label: "Xuất kho & bàn giao", path: "/warehouse/issues" },
            ],
          },
        ];

      case ROLES.OWNER:
        return [
          {
            heading: "ĐIỀU HÀNH & ĐƠN HÀNG",
            items: [
              { icon: Icons.dashboard, label: "Bảng điều hành & ETD", path: "/dashboard" },
              {
                icon: Icons.orders,
                label: "Danh sách đơn hàng",
                path: "/orders",
                children: orderSpaceChildItem ? [orderSpaceChildItem] : undefined,
              },
            ],
          },
          {
            heading: "KỸ THUẬT & QUY TRÌNH",
            items: [
              { icon: Icons.routing, label: "Cấu hình quy trình động", path: "/production/routing-config" },
              { icon: Icons.catalog, label: "Danh mục vật tư & sản phẩm", path: "/catalog" },
            ],
          },
          {
            heading: "TỒN KHO & MUA HÀNG",
            items: [
              { icon: Icons.inventory, label: "Tồn kho vật tư 3 cấp", path: "/inventory/stock" },
              { icon: Icons.purchasing, label: "Đơn đặt hàng (PO)", path: "/purchasing/orders" },
              { icon: Icons.audit, label: "Yêu cầu mua hàng (PR)", path: "/purchasing/requisitions" },
            ],
          },
          {
            heading: "XƯỞNG & KIỂM TOÁN",
            items: [
              { icon: Icons.kanban, label: "Tiến độ & Quy trình SX", path: "/production/kanban" },
              { icon: Icons.bolt, label: "Duyệt vượt chốt (Overdriver)", path: "/production/overdriver" },
              { icon: Icons.routing, label: "Duyệt lệnh sản xuất bù", path: "/production/replacement-approvals" },
              { icon: Icons.reports, label: "Báo cáo sản xuất & KCS", path: "/analytics/reports" },
              { icon: Icons.audit, label: "Nhật ký hoạt động", path: "/admin/audit-logs" },
            ],
          },
        ];

      case ROLES.SALES:
        return [
          {
            heading: "TIẾP NHẬN & QUẢN LÝ ĐƠN HÀNG",
            items: [
              {
                icon: Icons.orders,
                label: "Danh sách đơn hàng",
                path: "/orders",
                children: orderSpaceChildItem ? [orderSpaceChildItem] : undefined,
              },
              { icon: Icons.audit, label: "Lưới Staging PO Excel", path: "/orders/staging" },
            ],
          },
        ];

      case ROLES.PLANNER:
        return [
          {
            heading: "KẾ HOẠCH VẬT TƯ & BOM",
            items: [
              {
                icon: Icons.orders,
                label: "Danh sách đơn hàng",
                path: "/orders",
                children: orderSpaceChildItem ? [orderSpaceChildItem] : undefined,
              },
              { icon: Icons.catalog, label: "Danh mục vật tư & sản phẩm", path: "/catalog" },
              { icon: Icons.purchasing, label: "Đơn đặt hàng (PO)", path: "/purchasing/orders" },
              { icon: Icons.audit, label: "Yêu cầu mua hàng (PR)", path: "/purchasing/requisitions" },
            ],
          },
        ];

      case ROLES.PURCHASING:
        return [
          {
            heading: "NGHIỆP VỤ MUA HÀNG",
            items: [
              { icon: Icons.purchasing, label: "Đơn đặt hàng (PO)", path: "/purchasing/orders" },
              { icon: Icons.audit, label: "Yêu cầu mua hàng (PR)", path: "/purchasing/requisitions" },
              { icon: Icons.catalog, label: "Danh mục NCC & vật tư", path: "/catalog" },
            ],
          },
          {
            heading: "KHO & TỒN KHO",
            items: [
              { icon: Icons.inventory, label: "Tồn kho vật tư 3 cấp", path: "/inventory/stock" },
            ],
          },
        ];

      case ROLES.WAREHOUSE:
        return [
          {
            heading: "NGHIỆP VỤ KHO",
            items: [
              { icon: Icons.inventory, label: "Tồn kho vật tư 3 cấp", path: "/inventory/stock" },
              { icon: Icons.orders, label: "Nhập kho & xác nhận", path: "/warehouse/receipts" },
              { icon: Icons.space, label: "Xuất kho & bàn giao", path: "/warehouse/issues" },
              { icon: Icons.qc, label: "Nhập kho thành phẩm", path: "/warehouse/finished-goods" },
            ],
          },
          {
            heading: "DANH MỤC & XƯỞNG",
            items: [
              { icon: Icons.catalog, label: "Danh mục vật tư & sản phẩm", path: "/catalog" },
              { icon: Icons.kanban, label: "Tiến độ & Quy trình SX", path: "/production/kanban" },
            ],
          },
        ];

      case ROLES.LEADER:
        return [
          {
            heading: "XƯỞNG SẢN XUẤT",
            items: [
              { icon: Icons.dashboard, label: "Bảng điều hành & ETD", path: "/dashboard" },
              { icon: Icons.kanban, label: "Tiến độ & Quy trình SX", path: "/production/kanban" },
              { icon: Icons.routing, label: "Tiến độ công đoạn tại trạm", path: "/production/progress" },
              ...(orderSpaceChildItem
                ? [{ icon: Icons.space, label: orderSpaceChildItem.label, path: orderSpaceChildItem.path }]
                : []),
            ],
          },
          {
            heading: "PHÊ DUYỆT & PHÂN TÍCH",
            items: [
              { icon: Icons.bolt, label: "Yêu cầu vượt chốt (Overdriver)", path: "/production/overdriver" },
              { icon: Icons.reports, label: "Báo cáo sản xuất & KCS", path: "/analytics/reports" },
            ],
          },
        ];

      case ROLES.QC:
        return [
          {
            heading: "KIỂM SOÁT CHẤT LƯỢNG",
            items: [
              { icon: Icons.qc, label: "Kiểm tra QC & nhật ký lỗi", path: "/qc/inspection" },
              { icon: Icons.kanban, label: "Tiến độ & Quy trình SX", path: "/production/kanban" },
            ],
          },
        ];

      default:
        return [
          {
            heading: "MÀN HÌNH NGHIỆP VỤ",
            items: [
              {
                icon: Icons.orders,
                label: "Danh sách đơn hàng",
                path: "/orders",
                children: orderSpaceChildItem ? [orderSpaceChildItem] : undefined,
              },
            ],
          },
        ];
    }
  };

  const isItemActive = (path: string): boolean => {
    // 1. Trang lập BOM & quy trình
    if (path.startsWith("/bom")) {
      return location.pathname.startsWith("/bom");
    }

    // 2. Trang chi tiết Không gian đơn hàng
    if (path.includes("/orders/spaces/")) {
      return location.pathname.startsWith("/orders/spaces");
    }

    // 2b. Trang Đối chiếu tồn kho
    if (path.startsWith("/inventory/reconciliation")) {
      return location.pathname.startsWith("/inventory/reconciliation");
    }

    // 2c. Trang Tiến trình sản xuất
    if (path.startsWith("/production/progress")) {
      return (
        location.pathname.startsWith("/production/progress") ||
        location.pathname.startsWith("/production/kanban")
      );
    }

    // 3. Trang Danh sách đơn hàng: chỉ kích hoạt khi đúng URL danh sách, không nhận vơ các trang con /spaces hay /staging
    if (path === "/orders") {
      return location.pathname === "/orders" || location.pathname === "/orders/";
    }

    // 4. Các đường dẫn thông thường khác
    return location.pathname === path || (path !== "/" && location.pathname.startsWith(path + "/"));
  };

  // Hàm kiểm tra xem một item hoặc bất kỳ con/cháu nào của nó đang active
  const hasActiveDescendant = (item: SidebarItem): boolean => {
    if (!item.children || item.children.length === 0) return false;
    return item.children.some(
      (child) => isItemActive(child.path) || hasActiveDescendant(child)
    );
  };

  // Render một item trên cây Sidebar (hỗ trợ phân cấp nhiều tầng: cấp 0, cấp 1, cấp 2)
  const renderSidebarItemNode = (item: SidebarItem, level = 0): ReactNode => {
    const active = isItemActive(item.path);
    const hasDescendant = hasActiveDescendant(item);

    const isTopLevel = level === 0;
    const isLevel1 = level === 1;

    return (
      <li key={item.path} className="space-y-1">
        <button
          type="button"
          title={item.label}
          onClick={() => {
            navigate(item.path);
            onCloseMobile();
          }}
          className={`w-full flex items-center transition-colors cursor-pointer ${
            isTopLevel
              ? `gap-3 px-3 py-2.5 rounded-lg text-sm ${
                  active
                    ? "bg-white text-blue-700 font-semibold border-l-4 border-blue-600 shadow-xs"
                    : hasDescendant
                    ? "bg-slate-200/60 text-slate-900 font-semibold"
                    : "text-slate-600 hover:bg-slate-200/70 hover:text-slate-900 font-medium"
                }`
              : `gap-2 px-2.5 py-2 rounded-lg text-xs ${
                  active && !hasDescendant
                    ? "bg-white text-blue-700 font-bold border border-blue-200 shadow-xs"
                    : hasDescendant
                    ? "bg-slate-200/60 text-slate-900 font-semibold"
                    : "text-slate-600 hover:bg-slate-200/70 hover:text-slate-900 font-medium"
                }`
          }`}
        >
          {!isTopLevel && (
            <span className={active || hasDescendant ? "text-blue-600" : "text-slate-400"}>
              {Icons.subItem}
            </span>
          )}
          <span
            className={
              active
                ? "text-blue-600"
                : hasDescendant
                ? "text-slate-800"
                : "text-slate-500"
            }
          >
            {item.icon}
          </span>
          <span className="truncate">{item.label}</span>
        </button>

        {item.children && item.children.length > 0 && (
          <ul
            className={`mt-1 space-y-1 border-l-2 ${
              isTopLevel
                ? "ml-4 pl-2.5 border-blue-300"
                : isLevel1
                ? "ml-4 pl-2.5 border-blue-200"
                : "ml-3 pl-2 border-slate-300"
            }`}
          >
            {item.children.map((child) => renderSidebarItemNode(child, level + 1))}
          </ul>
        )}
      </li>
    );
  };

  const sidebarContent = (
    <>
      <div>
        {/* Brand Header */}
        <div className="h-16 px-5 flex items-center justify-between border-b border-slate-200">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-blue-600 flex items-center justify-center text-white font-extrabold text-sm shadow-xs select-none">
              W
            </div>
            <div>
              <div className="text-slate-900 font-extrabold tracking-tight text-sm leading-tight">
                WMMSWOOD
              </div>
              <div className="text-[11px] text-slate-500 font-medium">
                Sản xuất nội thất
              </div>
            </div>
          </div>
          <span
            className={`text-[11px] font-bold px-2 py-0.5 rounded uppercase tracking-wider border ${roleMeta.pillClass}`}
          >
            {roleMeta.badgeLabel}
          </span>
        </div>

        {/* Navigation Links (STRUCT-02) */}
        <nav className="py-5 px-3 space-y-6 overflow-y-auto max-h-[calc(100vh-8.5rem)]">
          {getSidebarSections().map((section, idx) => (
            <div key={idx}>
              <div className="px-3 mb-2 text-xs font-bold uppercase tracking-wider text-slate-500">
                {section.heading}
              </div>
              <ul className="space-y-1">
                {section.items.map((item) => renderSidebarItemNode(item, 0))}
              </ul>
            </div>
          ))}
        </nav>
      </div>

      {/* Sidebar Footer */}
      <div className="px-5 py-4 border-t border-slate-200 text-xs text-slate-500 space-y-1">
        <div className="font-semibold text-slate-700">Hệ thống WMMS v2.6.4</div>
        <div className="truncate" title={roleMeta.roleTitle}>
          Quản lý sản xuất xưởng gỗ
        </div>
      </div>
    </>
  );

  return (
    <>
      {/* Desktop Left Sidebar - Soft Cool Gray (COLOR-02) with Resizable Right Border */}
      <aside
        style={{ width: `${sidebarWidth}px` }}
        className="relative hidden lg:flex bg-[#F1F5F9] text-slate-700 flex-col justify-between shrink-0 select-none border-r border-slate-200 transition-[width] duration-75 ease-out"
      >
        {sidebarContent}

        {/* Cạnh phải kéo thả thay đổi độ rộng (Drag Handle) */}
        <div
          role="separator"
          tabIndex={0}
          aria-label="Kéo cạnh phải để thay đổi độ rộng menu điều hướng"
          aria-valuenow={sidebarWidth}
          aria-valuemin={MIN_SIDEBAR_WIDTH}
          aria-valuemax={MAX_SIDEBAR_WIDTH}
          onMouseDown={handleStartResize}
          onDoubleClick={handleResetWidth}
          title="Nhấn giữ chuột để kéo thay đổi độ rộng (nhấp đúp để đặt lại mặc định 280px)"
          className={`absolute top-0 -right-1 bottom-0 w-2.5 z-20 cursor-col-resize group transition-colors flex items-center justify-center ${
            isResizing ? "bg-blue-500/20" : "hover:bg-blue-400/20"
          }`}
        >
          <div
            className={`w-[3px] h-8 rounded-full transition-colors ${
              isResizing ? "bg-blue-600" : "bg-transparent group-hover:bg-blue-500"
            }`}
          />
        </div>
      </aside>

      {/* Mobile Sidebar Drawer Overlay (A11Y-04) */}
      {mobileOpen && (
        <div className="fixed inset-0 z-50 lg:hidden flex animate-in fade-in duration-200">
          <div
            className="fixed inset-0 bg-slate-900/50 backdrop-blur-xs"
            onClick={onCloseMobile}
            aria-label="Đóng menu điều hướng"
          />
          <aside className="relative z-10 w-64 bg-[#F1F5F9] text-slate-700 flex flex-col justify-between shrink-0 select-none border-r border-slate-200 h-full shadow-xl">
            {sidebarContent}
          </aside>
        </div>
      )}
    </>
  );
}
