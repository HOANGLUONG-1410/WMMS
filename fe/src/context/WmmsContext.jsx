import React, { createContext, useContext, useState } from 'react';
import {
  DEFAULT_ORDER_SPACES,
  INITIAL_AUDIT_LOGS,
  INITIAL_USERS_DIRECTORY,
  ROLE_CONFIG
} from '../constant/wmmsConstants';
import { authApi } from '../services/wmmsApi';

/**
 * Context Quản lý Phiên đăng nhập, Đa vai trò (Multi-Role RBAC - BR-ADM-01)
 * và Không gian Đơn hàng đang mở (Active Order Space - Section III.2, III.3, III.4).
 */
const WmmsContext = createContext(null);

export function WmmsProvider({ children }) {
  // Người dùng hiện tại và vai trò đang kích hoạt (Global Role Switcher)
  const [currentUser, setCurrentUser] = useState({
    userId: 1,
    username: 'admin',
    email: 'admin@nguyenkhoa.vn',
    fullName: 'Le Van Admin',
    initials: 'AD',
    activeRole: 'ADMIN',
    roles: ['ADMIN', 'OWNER', 'SALES', 'PLANNER', 'WAREHOUSE', 'PURCHASING', 'LEADER', 'QC']
  });

  // Không gian đơn hàng hiện tại (Current Space dropdown trên Header)
  const [orderSpaces, setOrderSpaces] = useState(DEFAULT_ORDER_SPACES);
  const [currentSpaceKey, setCurrentSpaceKey] = useState('SPACE-PO-880504');

  // State chia sẻ cho Employee User Directory (SCR-ADM-01) và Audit Logs (SCR-ADM-02)
  const [usersDirectory, setUsersDirectory] = useState(INITIAL_USERS_DIRECTORY);
  const [auditLogs, setAuditLogs] = useState(INITIAL_AUDIT_LOGS);

  /**
   * UC-01: Xử lý đăng nhập vào WMMS Production Suite
   * Tự động nhận diện vai trò dựa theo email/username hoặc gọi Backend API /api/v1/auth/login
   */
  const loginWorkspace = async (identifier, password) => {
    try {
      const response = await authApi.login(identifier, password, true);
      if (response?.success && response?.data) {
        const { accessToken, user } = response.data;
        if (accessToken) {
          localStorage.setItem('wmms_access_token', accessToken);
        }
        const primaryRole = user?.roles?.[0] || 'OWNER';
        const roleMeta = ROLE_CONFIG[primaryRole] || ROLE_CONFIG.OWNER;
        setCurrentUser({
          userId: user.userId,
          username: user.username,
          email: user.email,
          fullName: user.fullName || roleMeta.defaultUser.fullName,
          initials: getInitials(user.fullName || roleMeta.defaultUser.fullName),
          activeRole: primaryRole,
          roles: user.roles?.length ? user.roles : ['OWNER']
        });
        return { success: true, role: primaryRole, message: 'Sign in successful! Welcome back to WMMS. (MSG-AUT-01)' };
      }
    } catch {
      // Fallback sang chế độ mô phỏng theo email nếu chưa bật SQL Server local
    }

    const lower = String(identifier || '').trim().toLowerCase();
    let detectedRole = 'OWNER';
    if (lower.includes('admin')) detectedRole = 'ADMIN';
    else if (lower.includes('sales') || lower.includes('mai')) detectedRole = 'SALES';
    else if (lower.includes('warehouse') || lower.includes('thang')) detectedRole = 'WAREHOUSE';
    else if (lower.includes('purchas') || lower.includes('huy')) detectedRole = 'PURCHASING';
    else if (lower.includes('leader') || lower.includes('tien')) detectedRole = 'LEADER';
    else if (lower.includes('qc') || lower.includes('lan')) detectedRole = 'QC';
    else if (lower.includes('plan')) detectedRole = 'PLANNER';

    const meta = ROLE_CONFIG[detectedRole] || ROLE_CONFIG.OWNER;
    setCurrentUser({
      userId: 1,
      username: lower.split('@')[0] || 'owner',
      email: identifier,
      fullName: meta.defaultUser.fullName,
      initials: meta.defaultUser.initials,
      activeRole: detectedRole,
      roles: ['ADMIN', 'OWNER', 'SALES', 'PLANNER', 'WAREHOUSE', 'PURCHASING', 'LEADER', 'QC']
    });

    return {
      success: true,
      role: detectedRole,
      message: 'Sign in successful! Welcome back to WMMS. (MSG-AUT-01)'
    };
  };

  /**
   * Chuyển đổi nhanh ngữ cảnh vai trò làm việc (Multi-Role RBAC - BR-ADM-01)
   */
  const switchActiveRole = (roleCode) => {
    const meta = ROLE_CONFIG[roleCode] || ROLE_CONFIG.ADMIN;
    setCurrentUser((prev) => ({
      ...prev,
      activeRole: roleCode,
      fullName: meta.defaultUser.fullName,
      initials: meta.defaultUser.initials,
      email: meta.defaultUser.email
    }));
  };

  /**
   * UC-02: Đăng xuất khỏi hệ thống
   */
  const logoutWorkspace = async () => {
    try {
      await authApi.logout();
    } catch {
      // Bỏ qua lỗi mạng khi đăng xuất
    }
    localStorage.removeItem('wmms_access_token');
  };

  /**
   * Ghi thêm một bản ghi vào Immutable System Audit Logs (BR-ADM-03)
   */
  const appendAuditLog = (actionType, targetEntity, stateDelta, reason) => {
    const nowStr = new Date().toISOString().replace('T', ' ').slice(0, 19);
    const newEntry = {
      logId: Date.now(),
      timestamp: nowStr,
      actorName: currentUser.fullName,
      actorRole: currentUser.activeRole,
      actionType,
      targetEntity,
      stateDelta,
      reason: `"${reason}"`
    };
    setAuditLogs((prev) => [newEntry, ...prev]);
  };

  return (
    <WmmsContext.Provider
      value={{
        currentUser,
        setCurrentUser,
        switchActiveRole,
        loginWorkspace,
        logoutWorkspace,
        orderSpaces,
        setOrderSpaces,
        currentSpaceKey,
        setCurrentSpaceKey,
        usersDirectory,
        setUsersDirectory,
        auditLogs,
        setAuditLogs,
        appendAuditLog
      }}
    >
      {children}
    </WmmsContext.Provider>
  );
}

export function useWmms() {
  const ctx = useContext(WmmsContext);
  if (!ctx) {
    throw new Error('useWmms must be used within a WmmsProvider');
  }
  return ctx;
}

function getInitials(fullName = '') {
  const parts = fullName.trim().split(/\s+/);
  if (parts.length >= 2) {
    return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
  }
  return fullName.slice(0, 2).toUpperCase() || 'WM';
}
