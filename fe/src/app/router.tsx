import { useEffect } from "react";
import {
  createBrowserRouter,
  Link,
  Navigate,
  Outlet,
  useNavigate,
} from "react-router-dom";
import { setNavigate } from "../lib/navigation";
import { ProtectedRoute } from "./ProtectedRoute";
import { ROLES } from "../constants/roles";
import { useSession } from "../features/auth/store/SessionContext";
import { getDefaultRouteForRole } from "../components/layout/WmmsAppShell";

// Built Pages
import LoginPage from "../features/auth/pages/LoginPage";
import ResetPasswordPage from "../features/auth/pages/ResetPasswordPage";
import UserManagementPage from "../features/users/pages/UserManagementPage";
import SystemAuditLogPage from "../features/audit-logs/pages/SystemAuditLogPage";
import SalesOrderListPage from "../features/orders/pages/SalesOrderListPage";
import StagingGridPage from "../features/staging/pages/StagingGridPage";
import OrderSpaceDetailPage from "../features/orders/pages/OrderSpaceDetailPage";
import CatalogPage from "../features/catalog/pages/CatalogPage";
import BomBuilderPage from "../features/bom/pages/BomBuilderPage";
import PurchaseOrderManagementPage from "../features/purchasing/pages/PurchaseOrderManagementPage";
import InventoryStockPage from "../features/inventory/pages/InventoryStockPage";

// Placeholder Page for Screens Under Development
import UnderConstructionPage from "../components/common/UnderConstructionPage";

function RootLayout() {
  const navigate = useNavigate();

  useEffect(() => {
    setNavigate((to: string) => navigate(to));
  }, [navigate]);

  return <Outlet />;
}

/** Điều hướng thông minh tại trang gốc "/" theo vai trò đang kích hoạt của người dùng */
function RootRedirect() {
  const { currentUser, isInitializing } = useSession();

  if (isInitializing) {
    return (
      <div className="min-h-screen bg-slate-900 flex flex-col items-center justify-center">
        <div className="w-10 h-10 border-4 border-amber-500 border-t-transparent rounded-full animate-spin"></div>
      </div>
    );
  }

  if (!currentUser) {
    return <Navigate to="/login" replace />;
  }

  return <Navigate to={getDefaultRouteForRole(currentUser.activeRole)} replace />;
}

function ForbiddenPage() {
  return (
    <div className="min-h-screen bg-slate-900 text-white flex flex-col items-center justify-center p-6 text-center select-none">
      <div className="w-16 h-16 rounded-2xl bg-rose-500/10 border border-rose-500/20 flex items-center justify-center text-3xl mb-4 shadow-lg">
        🚫
      </div>
      <h1 className="text-2xl font-bold tracking-tight text-white mb-2">
        403 - Quyền Truy Cập Bị Giới Hạn
      </h1>
      <p className="text-slate-400 max-w-md text-sm mb-6 leading-relaxed">
        Vai trò đang kích hoạt của bạn không có thẩm quyền truy cập vào màn hình chức năng này theo quy định đặc tả Report 3.
      </p>
      <Link
        to="/"
        className="px-5 py-2.5 rounded-lg bg-[#2563eb] hover:bg-blue-700 text-white font-semibold text-xs shadow-sm transition"
      >
        Về Trang Chủ Hoạt Động
      </Link>
    </div>
  );
}

export const router = createBrowserRouter([
  {
    element: <RootLayout />,
    children: [
      // Trang điều hướng thông minh theo vai trò
      { path: "/", element: <RootRedirect /> },

      // Public Routes
      { path: "/login", element: <LoginPage /> },
      { path: "/reset-password", element: <ResetPasswordPage /> },
      { path: "/forbidden", element: <ForbiddenPage /> },

      // ==========================================
      // 1. Phân hệ Quản trị hệ thống & RBAC (ADMIN)
      // ==========================================
      {
        element: <ProtectedRoute allowedRoles={[ROLES.ADMIN]} />,
        children: [
          { path: "/admin/users", element: <UserManagementPage /> },
        ],
      },
      {
        element: <ProtectedRoute allowedRoles={[ROLES.ADMIN, ROLES.OWNER]} />,
        children: [
          { path: "/admin/audit-logs", element: <SystemAuditLogPage /> },
        ],
      },

      // ==========================================
      // 2. Phân hệ Đơn hàng & Staging (SALES, OWNER, ADMIN)
      // ==========================================
      {
        element: <ProtectedRoute allowedRoles={[ROLES.SALES, ROLES.OWNER, ROLES.ADMIN]} />,
        children: [
          { path: "/orders", element: <SalesOrderListPage /> },
        ],
      },
      {
        element: <ProtectedRoute allowedRoles={[ROLES.SALES, ROLES.OWNER, ROLES.ADMIN]} />,
        children: [
          { path: "/orders/staging", element: <StagingGridPage /> },
        ],
      },
      {
        element: <ProtectedRoute allowedRoles={[ROLES.SALES, ROLES.OWNER, ROLES.LEADER, ROLES.ADMIN]} />,
        children: [
          { path: "/orders/spaces/:spaceKey", element: <OrderSpaceDetailPage /> },
        ],
      },

      // ==========================================
      // 3. Phân hệ Kỹ thuật, BOM & Định tuyến động (OWNER, PLANNER, SALES, ADMIN)
      // ==========================================
      {
        element: <ProtectedRoute allowedRoles={[ROLES.OWNER, ROLES.PLANNER, ROLES.SALES, ROLES.ADMIN]} />,
        children: [
          { path: "/bom", element: <BomBuilderPage /> },
        ],
      },
      {
        element: <ProtectedRoute allowedRoles={[ROLES.OWNER, ROLES.ADMIN]} />,
        children: [
          {
            path: "/production/routing-config",
            element: (
              <UnderConstructionPage
                title="Dynamic Routing Config"
                screenCode="SCR-PRD-04"
                moduleName="Technical & Routing"
                allowedRoles={[ROLES.OWNER, ROLES.ADMIN]}
              />
            ),
          },
          {
            path: "/catalog/visual",
            element: (
              <UnderConstructionPage
                title="Visual Product Catalog"
                screenCode="SCR-CAT-02"
                moduleName="Technical & Routing"
                allowedRoles={[ROLES.OWNER, ROLES.ADMIN]}
              />
            ),
          },
        ],
      },
      {
        element: (
          <ProtectedRoute
            allowedRoles={[
              ROLES.OWNER,
              ROLES.PURCHASING,
              ROLES.WAREHOUSE,
              ROLES.PLANNER,
              ROLES.ADMIN,
            ]}
          />
        ),
        children: [
          { path: "/catalog", element: <CatalogPage /> },
        ],
      },

      // ==========================================
      // 4. Phân hệ Tồn kho & Đối soát (WAREHOUSE, PURCHASING, OWNER, PLANNER, SALES, ADMIN)
      // ==========================================
      {
        element: (
          <ProtectedRoute
            allowedRoles={[
              ROLES.WAREHOUSE,
              ROLES.PURCHASING,
              ROLES.OWNER,
              ROLES.PLANNER,
              ROLES.ADMIN,
            ]}
          />
        ),
        children: [
          { path: "/inventory/stock", element: <InventoryStockPage /> },
          { path: "/inventory/stocks", element: <InventoryStockPage /> },
          { path: "/inventory", element: <InventoryStockPage /> },
          { path: "/warehouse/stocks", element: <InventoryStockPage /> },
        ],
      },
      {
        element: (
          <ProtectedRoute
            allowedRoles={[
              ROLES.SALES,
              ROLES.OWNER,
              ROLES.PLANNER,
              ROLES.ADMIN,
            ]}
          />
        ),
        children: [
          {
            path: "/inventory/reconciliation",
            element: (
              <UnderConstructionPage
                title="Inventory Reconciliation & Allocation View"
                screenCode="SCR-INV-02"
                moduleName="Inventory & Stock"
                allowedRoles={[
                  ROLES.SALES,
                  ROLES.OWNER,
                  ROLES.PLANNER,
                  ROLES.ADMIN,
                ]}
              />
            ),
          },
        ],
      },

      // ==========================================
      // 5. Phân hệ Thu mua PO & PR (PURCHASING, OWNER, WAREHOUSE, PLANNER, ADMIN)
      // ==========================================
      {
        element: (
          <ProtectedRoute
            allowedRoles={[
              ROLES.PURCHASING,
              ROLES.OWNER,
              ROLES.WAREHOUSE,
              ROLES.PLANNER,
              ROLES.ADMIN,
            ]}
          />
        ),
        children: [
          {
            path: "/purchasing/orders",
            element: <PurchaseOrderManagementPage />,
          },
        ],
      },
      {
        element: (
          <ProtectedRoute
            allowedRoles={[
              ROLES.PURCHASING,
              ROLES.OWNER,
              ROLES.PLANNER,
              ROLES.ADMIN,
            ]}
          />
        ),
        children: [
          {
            path: "/purchasing/requisitions",
            element: (
              <UnderConstructionPage
                title="Consolidated Daily Purchase Requisition"
                screenCode="SCR-PUR-01"
                moduleName="Purchasing Operations"
                allowedRoles={[
                  ROLES.PURCHASING,
                  ROLES.OWNER,
                  ROLES.PLANNER,
                  ROLES.ADMIN,
                ]}
              />
            ),
          },
        ],
      },

      // ==========================================
      // 6. Phân hệ Thủ kho (WAREHOUSE, ADMIN)
      // ==========================================
      {
        element: <ProtectedRoute allowedRoles={[ROLES.WAREHOUSE, ROLES.ADMIN]} />,
        children: [
          {
            path: "/warehouse/receipts",
            element: (
              <UnderConstructionPage
                title="Goods Receipt & Commit Screen"
                screenCode="SCR-WAR-01"
                moduleName="Warehouse Operations"
                allowedRoles={[ROLES.WAREHOUSE, ROLES.ADMIN]}
              />
            ),
          },
          {
            path: "/warehouse/issues",
            element: (
              <UnderConstructionPage
                title="Goods Issue & Floor Handover"
                screenCode="SCR-WAR-02"
                moduleName="Warehouse Operations"
                allowedRoles={[ROLES.WAREHOUSE, ROLES.ADMIN]}
              />
            ),
          },
          {
            path: "/warehouse/finished-goods",
            element: (
              <UnderConstructionPage
                title="Finished Goods Receipt & Close Space"
                screenCode="SCR-WAR-03"
                moduleName="Warehouse Operations"
                allowedRoles={[ROLES.WAREHOUSE, ROLES.ADMIN]}
              />
            ),
          },
        ],
      },

      // ==========================================
      // 7. Phân hệ Điều hành & Sản xuất xưởng (OWNER, LEADER, QC, WAREHOUSE, ADMIN)
      // ==========================================
      {
        element: (
          <ProtectedRoute allowedRoles={[ROLES.OWNER, ROLES.LEADER, ROLES.ADMIN]} />
        ),
        children: [
          {
            path: "/dashboard",
            element: (
              <UnderConstructionPage
                title="Executive Dashboard & ETD Risk Alerts"
                screenCode="SCR-DSH-01"
                moduleName="Executive & Production"
                allowedRoles={[ROLES.OWNER, ROLES.LEADER, ROLES.ADMIN]}
              />
            ),
          },
          {
            path: "/production/overdriver",
            element: (
              <UnderConstructionPage
                title="Overdriver Request & Approval"
                screenCode="SCR-PRD-03"
                moduleName="Production Floor"
                allowedRoles={[ROLES.LEADER, ROLES.OWNER, ROLES.ADMIN]}
              />
            ),
          },
          {
            path: "/analytics/reports",
            element: (
              <UnderConstructionPage
                title="Production & Quality Analytics Report"
                screenCode="SCR-RPT-01"
                moduleName="Analytics & Reports"
                allowedRoles={[ROLES.OWNER, ROLES.LEADER, ROLES.ADMIN]}
              />
            ),
          },
        ],
      },
      {
        element: (
          <ProtectedRoute
            allowedRoles={[
              ROLES.LEADER,
              ROLES.QC,
              ROLES.OWNER,
              ROLES.WAREHOUSE,
              ROLES.ADMIN,
            ]}
          />
        ),
        children: [
          {
            path: "/production/kanban",
            element: (
              <UnderConstructionPage
                title="Production Kanban Board"
                screenCode="SCR-PRD-01"
                moduleName="Production Floor"
                allowedRoles={[
                  ROLES.LEADER,
                  ROLES.QC,
                  ROLES.OWNER,
                  ROLES.WAREHOUSE,
                  ROLES.ADMIN,
                ]}
              />
            ),
          },
        ],
      },
      {
        element: <ProtectedRoute allowedRoles={[ROLES.LEADER, ROLES.ADMIN]} />,
        children: [
          {
            path: "/production/progress",
            element: (
              <UnderConstructionPage
                title="Workstation Stage Progress"
                screenCode="SCR-PRD-02"
                moduleName="Production Floor"
                allowedRoles={[ROLES.LEADER, ROLES.ADMIN]}
              />
            ),
          },
        ],
      },
      {
        element: <ProtectedRoute allowedRoles={[ROLES.OWNER, ROLES.ADMIN]} />,
        children: [
          {
            path: "/production/replacement-approvals",
            element: (
              <UnderConstructionPage
                title="Replacement Production Approval"
                screenCode="SCR-PRD-05"
                moduleName="Production Floor"
                allowedRoles={[ROLES.OWNER, ROLES.ADMIN]}
              />
            ),
          },
        ],
      },

      // ==========================================
      // 8. Phân hệ Quản lý Chất lượng QC (QC, ADMIN)
      // ==========================================
      {
        element: <ProtectedRoute allowedRoles={[ROLES.QC, ROLES.ADMIN]} />,
        children: [
          {
            path: "/qc/inspection",
            element: (
              <UnderConstructionPage
                title="QC Inspection & Defect Logging"
                screenCode="SCR-QC-01"
                moduleName="Quality Control"
                allowedRoles={[ROLES.QC, ROLES.ADMIN]}
              />
            ),
          },
        ],
      },

      // Fallback
      { path: "*", element: <Navigate to="/" replace /> },
    ],
  },
]);
