import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useSession } from "../features/auth/store/SessionContext";
import type { RoleCode } from "../constants/roles";

interface Props {
  allowedRoles?: RoleCode[]; // omit = any authenticated user
}

function FullPageSpinner() {
  return (
    <div className="min-h-screen bg-slate-900 flex flex-col items-center justify-center">
      <div className="w-10 h-10 border-4 border-amber-500 border-t-transparent rounded-full animate-spin"></div>
      <p className="mt-4 text-xs font-semibold tracking-widest text-slate-400 uppercase">
        Verifying Session...
      </p>
    </div>
  );
}

export function ProtectedRoute({ allowedRoles }: Props) {
  const { currentUser, isInitializing } = useSession();
  const location = useLocation();

  // Đang khôi phục session (F5 reload / silent refresh)
  if (isInitializing) {
    return <FullPageSpinner />;
  }

  // Chưa đăng nhập -> điều hướng về /login và lưu location để redirect lại sau khi login thành công
  if (!currentUser) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  // Đã đăng nhập nhưng activeRole không nằm trong danh sách quyền cho phép
  if (allowedRoles && !allowedRoles.includes(currentUser.activeRole)) {
    return <Navigate to="/forbidden" replace />;
  }

  return <Outlet />;
}
