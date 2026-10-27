import { createContext, useContext, useState, type ReactNode } from "react";
import {
  DEFAULT_ORDER_SPACES,
  INITIAL_AUDIT_LOGS,
  INITIAL_USERS_DIRECTORY,
  ROLE_CONFIG,
} from "../../../constants/wmmsConstants";

/**
 * Context Quản lý Dữ liệu Mẫu (Mock Store) cho các màn hình nghiệp vụ:
 * - Order Spaces (Section III.4): Danh sách không gian đơn hàng
 * - Users Directory (SCR-ADM-01): Danh mục nhân sự
 * - System Audit Logs (SCR-ADM-02): Nhật ký hệ thống
 */

interface WmmsContextType {
  currentUser: CurrentUser;
  setCurrentUser: React.Dispatch<React.SetStateAction<CurrentUser>>;
  switchActiveRole: (roleCode: string) => void;
  logoutWorkspace: () => Promise<void>;
  orderSpaces: typeof DEFAULT_ORDER_SPACES;
  setOrderSpaces: React.Dispatch<
    React.SetStateAction<typeof DEFAULT_ORDER_SPACES>
  >;
  currentSpaceKey: string;
  setCurrentSpaceKey: React.Dispatch<React.SetStateAction<string>>;
  usersDirectory: typeof INITIAL_USERS_DIRECTORY;
  setUsersDirectory: React.Dispatch<
    React.SetStateAction<typeof INITIAL_USERS_DIRECTORY>
  >;
  auditLogs: AuditLogEntry[];
  setAuditLogs: React.Dispatch<React.SetStateAction<AuditLogEntry[]>>;
  appendAuditLog: (
    actionType: string,
    targetEntity: string,
    stateDelta: string,
    reason: string,
  ) => void;
}

interface CurrentUser {
  userId: number;
  username: string;
  email: string;
  fullName: string;
  initials: string;
  activeRole: string;
  roles: string[];
}

interface AuditLogEntry {
  logId: number;
  timestamp: string;
  actorName: string;
  actorRole: string;
  actionType: string;
  targetEntity: string;
  stateDelta: string;
  reason: string;
}

const WmmsContext = createContext<WmmsContextType | null>(null);

export type RoleCode = keyof typeof ROLE_CONFIG;

export function WmmsProvider({ children }: { children: ReactNode }) {
  // Người dùng mẫu phục vụ giao diện các trang nghiệp vụ chưa nối SessionContext
  const [currentUser, setCurrentUser] = useState<CurrentUser>({
    userId: 1,
    username: "admin",
    email: "admin@nguyenkhoa.vn",
    fullName: "Le Van Admin",
    initials: "LA",
    activeRole: "ADMIN",
    roles: [
      "ADMIN",
      "OWNER",
      "SALES",
      "PLANNER",
      "WAREHOUSE",
      "PURCHASING",
      "LEADER",
      "QC",
    ],
  });

  // Không gian đơn hàng hiện tại (Current Space dropdown trên Header)
  const [orderSpaces, setOrderSpaces] = useState(DEFAULT_ORDER_SPACES);
  const [currentSpaceKey, setCurrentSpaceKey] = useState("SPACE-PO-880504");

  // State chia sẻ cho Employee User Directory (SCR-ADM-01) và Audit Logs (SCR-ADM-02)
  const [usersDirectory, setUsersDirectory] = useState(INITIAL_USERS_DIRECTORY);
  const [auditLogs, setAuditLogs] = useState(INITIAL_AUDIT_LOGS);

  /**
   * Chuyển đổi nhanh ngữ cảnh vai trò làm việc trên Header (Multi-Role Switcher)
   */
  const switchActiveRole = (roleCode: string) => {
    const meta = ROLE_CONFIG[roleCode as RoleCode] || ROLE_CONFIG.ADMIN;
    setCurrentUser((prev) => ({
      ...prev,
      activeRole: roleCode,
      fullName: meta.defaultUser.fullName,
      initials: meta.defaultUser.initials,
      email: meta.defaultUser.email,
    }));
  };

  /**
   * Tương thích ngược với WmmsAppShell cho đến khi component này chuyển sang dùng SessionContext
   */
  const logoutWorkspace = async () => {};

  /**
   * Ghi thêm một bản ghi vào System Audit Logs
   */
  const appendAuditLog = (
    actionType: string,
    targetEntity: string,
    stateDelta: string,
    reason: string,
  ) => {
    const nowStr = new Date().toISOString().replace("T", " ").slice(0, 19);
    const newEntry: AuditLogEntry = {
      logId: Date.now(),
      timestamp: nowStr,
      actorName: currentUser.fullName,
      actorRole: currentUser.activeRole,
      actionType,
      targetEntity,
      stateDelta,
      reason: `"${reason}"`,
    };
    setAuditLogs((prev) => [newEntry, ...prev]);
  };

  return (
    <WmmsContext.Provider
      value={{
        currentUser,
        setCurrentUser,
        switchActiveRole,
        logoutWorkspace,
        orderSpaces,
        setOrderSpaces,
        currentSpaceKey,
        setCurrentSpaceKey,
        usersDirectory,
        setUsersDirectory,
        auditLogs,
        setAuditLogs,
        appendAuditLog,
      }}
    >
      {children}
    </WmmsContext.Provider>
  );
}

export function useWmms() {
  const ctx = useContext(WmmsContext);
  if (!ctx) {
    throw new Error("useWmms must be used within a WmmsProvider");
  }
  return ctx;
}
