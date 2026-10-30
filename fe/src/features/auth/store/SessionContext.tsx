import {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
  type ReactNode,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { authApi } from "../api/authApi";
import { setAccessToken } from "./tokenStore";
import type { AuthUser } from "../types";
import type { RoleCode } from "../../../constants/roles";

/** User đang đăng nhập + dữ liệu phái sinh phục vụ UI (initials, role đang kích hoạt). */
export type SessionUser = AuthUser & {
  initials: string;
  activeRole: RoleCode;
};

interface SessionValue {
  currentUser: SessionUser | null;
  isInitializing: boolean;
  login: (identifier: string, password: string) => Promise<SessionUser>;
  logout: () => Promise<void>;
  switchActiveRole: (role: RoleCode) => void;
}

/** Lấy chữ cái viết tắt của họ tên để hiển thị avatar (VD: "Le Van Admin" -> "LA"). */
export function getInitials(fullName = ""): string {
  const parts = fullName.trim().split(/\s+/);
  if (parts.length >= 2) {
    return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
  }
  return fullName.slice(0, 2).toUpperCase() || "WM";
}

/** Rút thông báo lỗi hiển thị cho người dùng từ lỗi trả về của API đăng nhập. */
export function getErrorMessage(error: unknown): string {
  if (error && typeof error === "object") {
    if (
      "errors" in error &&
      Array.isArray((error as { errors?: unknown }).errors)
    ) {
      const errs = (error as { errors: string[] }).errors;
      if (errs.length > 0 && errs[0]) return errs[0];
    }
    if (
      "message" in error &&
      typeof (error as { message?: unknown }).message === "string"
    ) {
      return (error as { message: string }).message;
    }
  }
  return "Invalid username/email or password (MSG02).";
}

function buildSessionUser(user: AuthUser): SessionUser {
  const roles: string[] = user?.roles ?? [];
  const fullName = user.fullName || user.username;
  return {
    ...user,
    fullName,
    initials: getInitials(fullName),
    activeRole: (roles[0] ?? "") as RoleCode,
    roles,
  };
}

const SessionContext = createContext<SessionValue | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const [currentUser, setCurrentUser] = useState<SessionUser | null>(null);
  const [isInitializing, setIsInitializing] = useState(true);
  const queryClient = useQueryClient();

  // Khôi phục session khi tải lại trang (F5):
  // 1. Gọi POST /auth/refresh (trình duyệt tự kẹp HttpOnly cookie wmms_refresh_token)
  // 2. Nhận accessToken mới -> lưu vào RAM (tokenStore)
  // 3. Gọi GET /auth/me để lấy profile user hiện tại
  useEffect(() => {
    let isMounted = true;

    const restoreSession = async () => {
      try {
        const refreshRes = await authApi.refreshToken("");
        const newAccessToken = refreshRes?.data?.accessToken;

        if (newAccessToken) {
          setAccessToken(newAccessToken);

          const meRes = await authApi.getMe();
          const userData = meRes?.data;
          if (isMounted && userData) {
            setCurrentUser(buildSessionUser(userData));
          }
        } else {
          setAccessToken(null);
          if (isMounted) setCurrentUser(null);
        }
      } catch {
        setAccessToken(null);
        if (isMounted) {
          setCurrentUser(null);
        }
      } finally {
        if (isMounted) {
          setIsInitializing(false);
        }
      }
    };

    restoreSession();

    return () => {
      isMounted = false;
    };
  }, []);

  const login = useCallback(async (identifier: string, password: string) => {
    const res = await authApi.login(identifier, password, true);
    const { accessToken, user } = res.data; // authApi đã return res.data (envelope)

    const roles: string[] = user?.roles ?? [];
    if (roles.length === 0) {
      throw new Error(
        "This account has no role assigned. Contact your administrator.",
      );
    }

    setAccessToken(accessToken); // Lưu accessToken vào tokenStore (RAM)

    const nextUser = buildSessionUser(user);
    setCurrentUser(nextUser);
    return nextUser;
  }, []);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } finally {
      setAccessToken(null);
      setCurrentUser(null);
      queryClient.clear(); // Xóa sạch TanStack Query cache để tránh rò rỉ dữ liệu phiên trước
    }
  }, [queryClient]);

  const switchActiveRole = useCallback((role: RoleCode) => {
    setCurrentUser((u) =>
      u && u.roles.includes(role) ? { ...u, activeRole: role } : u,
    );
  }, []);

  return (
    <SessionContext.Provider
      value={{ currentUser, isInitializing, login, logout, switchActiveRole }}
    >
      {children}
    </SessionContext.Provider>
  );
}

export function useSession() {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error("useSession must be used inside <SessionProvider>");
  return ctx;
}
