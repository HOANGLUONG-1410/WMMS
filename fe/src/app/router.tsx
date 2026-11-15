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
import PurchaseRequestPage from "../features/purchasing/pages/PurchaseRequestPage";
import GoodsReceiptPage from "../features/warehouse/pages/GoodsReceiptPage";
import GoodsIssuePage from "../features/warehouse/pages/GoodsIssuePage";
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
      <div className="min-h-screen bg-[#F8FAFC] flex flex-col items-center justify-center">
        <div className="w-10 h-10 border-4 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
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
    <div className="min-h-screen bg-[#F8FAFC] text-slate-800 flex flex-col items-center justify-center p-6 text-center select-none">
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-8 max-w-md w-full flex flex-col items-center">
        <div className="w-16 h-16 rounded-2xl bg-rose-50 border border-rose-200 flex items-center justify-center text-rose-600 mb-4">
          <svg className="w-8 h-8" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
          </svg>
        </div>
        <h1 className="text-xl font-extrabold tracking-tight text-slate-900 mb-2">
          Quyền truy cập bị giới hạn
        </h1>
        <p className="text-slate-600 text-sm mb-6 leading-relaxed">
          Vai trò đang kích hoạt của bạn không có quyền truy cập vào màn hình chức năng này.
        </p>
        <Link
          to="/"
          className="px-5 py-2.5 rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-semibold text-sm shadow-xs transition"
        >
          Về trang chủ hoạt động
        </Link>
      </div>
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
                title="Cấu hình quy trình động"
                screenCode="SCR-PRD-04"
                moduleName="Kỹ thuật & Quy trình"
                allowedRoles={[ROLES.OWNER, ROLES.ADMIN]}
              />
            ),
          },
          {
            path: "/catalog/visual",
            element: (
              <UnderConstructionPage
                title="Danh mục sản phẩm trực quan"
                screenCode="SCR-CAT-02"
                moduleName="Kỹ thuật & Quy trình"
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
                title="Đối chiếu tồn kho & phân bổ vật tư"
                screenCode="SCR-INV-02"
                moduleName="Tồn kho"
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
            element: <PurchaseRequestPage />,
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
            element: <GoodsReceiptPage />,
          },
          {
            path: "/warehouse/issues",
            element: <GoodsIssuePage />,
          },
          {
            path: "/warehouse/finished-goods",
            element: (
              <UnderConstructionPage
                title="Nhập kho thành phẩm & đóng Space"
                screenCode="SCR-WAR-03"
                moduleName="Nghiệp vụ kho"
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
                title="Bảng điều hành & cảnh báo rủi ro ETD"
                screenCode="SCR-DSH-01"
                moduleName="Điều hành & Sản xuất"
                allowedRoles={[ROLES.OWNER, ROLES.LEADER, ROLES.ADMIN]}
              />
            ),
          },
          {
            path: "/production/overdriver",
            element: (
              <UnderConstructionPage
                title="Yêu cầu & duyệt vượt chốt (Overdriver)"
                screenCode="SCR-PRD-03"
                moduleName="Xưởng sản xuất"
                allowedRoles={[ROLES.LEADER, ROLES.OWNER, ROLES.ADMIN]}
              />
            ),
          },
          {
            path: "/analytics/reports",
            element: (
              <UnderConstructionPage
                title="Báo cáo phân tích sản xuất & chất lượng"
                screenCode="SCR-RPT-01"
                moduleName="Phân tích & Báo cáo"
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
                title="Bảng Kanban sản xuất"
                screenCode="SCR-PRD-01"
                moduleName="Xưởng sản xuất"
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
                title="Tiến độ công đoạn tại trạm"
                screenCode="SCR-PRD-02"
                moduleName="Xưởng sản xuất"
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
                title="Duyệt lệnh sản xuất bù"
                screenCode="SCR-PRD-05"
                moduleName="Xưởng sản xuất"
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
                title="Kiểm tra QC & ghi nhận lỗi"
                screenCode="SCR-QC-01"
                moduleName="Kiểm soát chất lượng"
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
