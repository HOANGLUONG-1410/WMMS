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

// Pages
import LoginPage from "../features/auth/pages/LoginPage";
import ResetPasswordPage from "../features/auth/pages/ResetPasswordPage";
import UserManagementPage from "../features/users/pages/UserManagementPage";
import SystemAuditLogPage from "../features/audit-logs/pages/SystemAuditLogPage";
import SalesOrderListPage from "../features/orders/pages/SalesOrderListPage";
import StagingGridPage from "../features/staging/pages/StagingGridPage";
import OrderSpaceDetailPage from "../features/orders/pages/OrderSpaceDetailPage";

function RootLayout() {
  const navigate = useNavigate();

  useEffect(() => {
    setNavigate((to: string) => navigate(to));
  }, [navigate]);

  return <Outlet />;
}

function ForbiddenPage() {
  return (
    <div className="min-h-screen bg-slate-900 text-white flex flex-col items-center justify-center p-6 text-center select-none">
      <div className="w-16 h-16 rounded-2xl bg-rose-500/10 border border-rose-500/20 flex items-center justify-center text-3xl mb-4">
        🚫
      </div>
      <h1 className="text-2xl font-bold tracking-tight text-white mb-2">
        403 - Access Denied
      </h1>
      <p className="text-slate-400 max-w-md text-sm mb-6 leading-relaxed">
        Your active role does not have permission to access this operational screen.
        Please contact your administrator or switch to an authorized role.
      </p>
      <Link
        to="/login"
        className="px-5 py-2.5 rounded-lg bg-[#2563eb] hover:bg-blue-700 text-white font-semibold text-xs shadow-sm transition"
      >
        Back to Dashboard / Login
      </Link>
    </div>
  );
}

export const router = createBrowserRouter([
  {
    element: <RootLayout />,
    children: [
      // Public Routes
      { path: "/login", element: <LoginPage /> },
      { path: "/reset-password", element: <ResetPasswordPage /> },
      { path: "/forbidden", element: <ForbiddenPage /> },

      // Protected: Phân hệ Quản trị hệ thống (Chỉ ADMIN)
      {
        element: <ProtectedRoute allowedRoles={[ROLES.ADMIN]} />,
        children: [
          { path: "/admin/users", element: <UserManagementPage /> },
          { path: "/admin/audit-logs", element: <SystemAuditLogPage /> },
        ],
      },

      // Protected: Phân hệ Quản lý Đơn hàng & Staging PO (SALES, OWNER, ADMIN)
      {
        element: (
          <ProtectedRoute
            allowedRoles={[ROLES.ADMIN, ROLES.OWNER, ROLES.SALES]}
          />
        ),
        children: [
          { path: "/orders", element: <SalesOrderListPage /> },
          { path: "/orders/staging", element: <StagingGridPage /> },
        ],
      },

      // Protected: Mọi tài khoản đã đăng nhập
      {
        element: <ProtectedRoute />,
        children: [
          {
            path: "/orders/spaces/:spaceKey",
            element: <OrderSpaceDetailPage />,
          },
        ],
      },

      // Default fallback
      { path: "/", element: <Navigate to="/login" replace /> },
      { path: "*", element: <Navigate to="/login" replace /> },
    ],
  },
]);

