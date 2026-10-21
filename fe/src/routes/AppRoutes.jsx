import React from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { WmmsProvider } from '../context/WmmsContext';
import LoginPage from '../pages/LoginPage';
import UserManagementPage from '../pages/UserManagementPage';
import SystemAuditLogPage from '../pages/SystemAuditLogPage';
import SalesOrderListPage from '../pages/SalesOrderListPage';
import StagingGridPage from '../pages/StagingGridPage';
import OrderSpaceDetailPage from '../pages/OrderSpaceDetailPage';

/**
 * Cấu hình Định tuyến Ứng dụng WMMS Frontend (React Router v6)
 * Ánh xạ đầy đủ các màn hình trong Section III.1, III.2, III.3, III.4 của Report 3:
 * - `/login`                      -> III.1.1 Login & III.1.2 Reset Personal Password Modal
 * - `/admin/users`                -> III.3.1 User Accounts & Multi-Role RBAC (SCR-ADM-01) + III.3.2 Create User Modal (MOD-ADM-01) + III.3.3 Role Permission Matrix Modal (MOD-ADM-02)
 * - `/admin/audit-logs`           -> III.3.4 Immutable System Audit Logs (SCR-ADM-02)
 * - `/orders`                     -> III.4.1 Sales Orders Directory (SCR-ORD-01)
 * - `/orders/staging`             -> III.4.2 Safe Excel PO Staging Grid (SCR-ORD-02)
 * - `/orders/spaces/:spaceKey`    -> III.4.3 Order Space Detail Dashboard (SCR-ORD-03)
 */
export default function AppRoutes() {
  return (
    <WmmsProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Navigate to="/login" replace />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/admin/users" element={<UserManagementPage />} />
          <Route path="/admin/audit-logs" element={<SystemAuditLogPage />} />
          <Route path="/orders" element={<SalesOrderListPage />} />
          <Route path="/orders/staging" element={<StagingGridPage />} />
          <Route path="/orders/spaces/:spaceKey" element={<OrderSpaceDetailPage />} />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </BrowserRouter>
    </WmmsProvider>
  );
}
