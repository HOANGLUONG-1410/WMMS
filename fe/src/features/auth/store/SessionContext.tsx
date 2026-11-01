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

export interface LoginResult {
  user: SessionUser;
  message: string;
}

interface SessionValue {
  currentUser: SessionUser | null;
  isInitializing: boolean;
  login: (identifier: string, password: string) => Promise<LoginResult>;
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

/**
 * Rút thông báo lỗi hiển thị cho người dùng từ lỗi trả về của API:
 * 1. Ưu tiên cao nhất: message do Backend trả về.
 * 2. Ưu tiên nhì: mảng errors do Backend trả về (bỏ qua các mã code nội bộ như MSG01, MSG02).
 * 3. Fallback cuối cùng: thông báo mặc định của Frontend khi mất kết nối mạng.
 */
export function getErrorMessage(error: unknown): string {
  if (error && typeof error === "object") {
    const err = error as { message?: string; errors?: string[] };

    // 1. Ưu tiên cao nhất: Lấy chuỗi message do Backend trả về
    if (typeof err.message === "string" && err.message.trim().length > 0) {
      return err.message.trim();
    }

    // 2. Ưu tiên nhì: Lấy lỗi chi tiết từ backend (bỏ qua mã code nội bộ như MSG01, MSG02...)
    if (Array.isArray(err.errors) && err.errors.length > 0) {
      const fieldError = err.errors.find(
        (e) => typeof e === "string" && !/^MSG\d+$/i.test(e),
      );
      if (fieldError && fieldError.trim().length > 0) {
        return fieldError.trim();
      }
    }
  }

  // 3. Fallback cuối cùng nếu không nhận được bất kỳ phản hồi nào từ server
  return "Unable to connect to server. Please try again.";
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

  // Khôi phục session khi tải lại trang (F5)
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
    const { accessToken, user } = res.data;

    const roles: string[] = user?.roles ?? [];
    if (roles.length === 0) {
      throw new Error(
        "This account has no role assigned. Contact your administrator.",
      );
    }

    setAccessToken(accessToken);

    const nextUser = buildSessionUser(user);
    setCurrentUser(nextUser);
    return {
      user: nextUser,
      message: res.message || "Signed in successfully!",
    };
  }, []);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } finally {
      setAccessToken(null);
      setCurrentUser(null);
      queryClient.clear();
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
