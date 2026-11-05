import { useNavigate } from "react-router-dom";
import WmmsAppShell from "../layout/WmmsAppShell";
import { useSession } from "../../features/auth/store/SessionContext";
import { ROLE_CONFIG } from "../../constants/wmmsConstants";

interface UnderConstructionPageProps {
  title: string;
  screenCode?: string;
  moduleName?: string;
  allowedRoles?: string[];
  description?: string;
}

export default function UnderConstructionPage({
  title,
  moduleName = "Màn hình nghiệp vụ",
  allowedRoles = [],
  description = "Màn hình chức năng này đã được định nghĩa trong kiến trúc luồng màn hình. Giao diện chi tiết và API nghiệp vụ đang trong quá trình phát triển.",
}: UnderConstructionPageProps) {
  const navigate = useNavigate();
  const { currentUser } = useSession();

  const getDefaultHome = () => {
    switch (currentUser?.activeRole) {
      case "ADMIN":
        return "/admin/users";
      case "SALES":
        return "/orders";
      case "OWNER":
        return "/orders";
      case "PLANNER":
        return "/bom";
      case "PURCHASING":
        return "/purchasing/orders";
      case "WAREHOUSE":
        return "/inventory/stock";
      case "LEADER":
        return "/production/kanban";
      case "QC":
        return "/qc/inspection";
      default:
        return "/orders";
    }
  };

  return (
    <WmmsAppShell>
      <div className="max-w-4xl mx-auto py-10 px-4">
        {/* Breadcrumb / Top Tag */}
        <div className="flex items-center gap-2 mb-6 text-xs text-slate-500 font-medium">
          <span className="uppercase tracking-wider">{moduleName}</span>
          <span>/</span>
          <span className="text-slate-800 font-semibold">{title}</span>
        </div>

        {/* Main Card */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-8 text-center sm:text-left relative overflow-hidden">
          {/* Subtle Background Accent */}
          <div className="absolute top-0 right-0 w-48 h-48 bg-gradient-to-bl from-amber-500/10 via-amber-500/5 to-transparent rounded-bl-full pointer-events-none" />

          <div className="flex flex-col sm:flex-row items-center sm:items-start gap-6">
            {/* Construction Icon */}
            <div className="w-16 h-16 rounded-2xl bg-amber-50 border border-amber-200 flex items-center justify-center text-3xl shrink-0 shadow-inner">
              🚧
            </div>

            <div className="flex-1 space-y-3">
              <div className="flex flex-wrap items-center gap-2 justify-center sm:justify-start">
                <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-200">
                  <span className="w-2 h-2 rounded-full bg-amber-500 animate-pulse"></span>
                  Chưa được thiết kế UI / Đang phát triển
                </span>
                {currentUser?.activeRole && (
                  <span className="text-xs px-2.5 py-1 rounded-full bg-slate-100 text-slate-600 font-medium border border-slate-200">
                    Thẩm quyền truy cập: <b>{ROLE_CONFIG[currentUser.activeRole]?.displayName || currentUser.activeRole}</b>
                  </span>
                )}
              </div>

              <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
                {title}
              </h1>

              <p className="text-sm text-slate-600 leading-relaxed max-w-2xl">
                {description}
              </p>

              {/* Roles authorized info */}
              {allowedRoles.length > 0 && (
                <div className="pt-2 text-xs text-slate-500">
                  <span className="font-semibold text-slate-700">Các vai trò có thẩm quyền xem màn này: </span>
                  <div className="inline-flex flex-wrap gap-1.5 mt-1">
                    {allowedRoles.map((role) => (
                      <span
                        key={role}
                        className="px-2 py-0.5 rounded text-[11px] font-semibold bg-slate-100 text-slate-700 border border-slate-200"
                      >
                        {role}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              {/* Actions */}
              <div className="pt-4 flex flex-wrap items-center gap-3 justify-center sm:justify-start">
                <button
                  type="button"
                  onClick={() => navigate(-1)}
                  className="px-4 py-2 text-xs font-semibold rounded-lg border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 transition shadow-sm"
                >
                  ← Quay lại trang trước
                </button>
                <button
                  type="button"
                  onClick={() => navigate(getDefaultHome())}
                  className="px-4 py-2 text-xs font-semibold rounded-lg bg-[#0d1528] text-white hover:bg-slate-800 transition shadow-sm"
                >
                  Về trang chủ hoạt động
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </WmmsAppShell>
  );
}

