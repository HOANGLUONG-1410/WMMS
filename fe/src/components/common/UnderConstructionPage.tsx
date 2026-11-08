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
  description = "Chức năng nghiệp vụ này đang được đồng bộ dữ liệu vận hành trong phiên bản hiện tại.",
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
      <div className="max-w-4xl mx-auto py-8 px-4">
        {/* Breadcrumb */}
        <div className="flex items-center gap-2 mb-6 text-sm text-slate-500 font-medium">
          <span>{moduleName}</span>
          <span>/</span>
          <span className="text-slate-800 font-semibold">{title}</span>
        </div>

        {/* Main Card */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs p-8 text-center sm:text-left">
          <div className="flex flex-col sm:flex-row items-center sm:items-start gap-6">
            <div className="w-14 h-14 rounded-xl bg-blue-50 border border-blue-100 flex items-center justify-center text-blue-600 shrink-0">
              <svg className="w-7 h-7" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
              </svg>
            </div>

            <div className="flex-1 space-y-3">
              <div className="flex flex-wrap items-center gap-2 justify-center sm:justify-start">
                <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold uppercase bg-amber-50 text-amber-800 border border-amber-200">
                  <span className="w-1.5 h-1.5 rounded-full bg-amber-500"></span>
                  Đang cập nhật phân hệ
                </span>
                {currentUser?.activeRole && (
                  <span className="text-xs px-2.5 py-1 rounded-full bg-slate-100 text-slate-700 font-medium border border-slate-200">
                    Vai trò hiện tại: <b>{ROLE_CONFIG[currentUser.activeRole]?.displayName || currentUser.activeRole}</b>
                  </span>
                )}
              </div>

              <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
                {title}
              </h1>

              <p className="text-sm text-slate-600 leading-relaxed max-w-2xl">
                {description}
              </p>

              {allowedRoles.length > 0 && (
                <div className="pt-2 text-xs text-slate-500">
                  <span className="font-semibold text-slate-700">Bộ phận được phân quyền: </span>
                  <div className="inline-flex flex-wrap gap-1.5 mt-1">
                    {allowedRoles.map((role) => (
                      <span
                        key={role}
                        className="px-2.5 py-0.5 rounded text-xs font-bold bg-slate-100 text-slate-700 border border-slate-200"
                      >
                        {role}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              <div className="pt-4 flex flex-wrap items-center gap-3 justify-center sm:justify-start">
                <button
                  type="button"
                  onClick={() => navigate(-1)}
                  className="px-4 py-2.5 text-sm font-semibold rounded-lg border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 transition shadow-xs cursor-pointer"
                >
                  Quay lại trang trước
                </button>
                <button
                  type="button"
                  onClick={() => navigate(getDefaultHome())}
                  className="px-4 py-2.5 text-sm font-semibold rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition shadow-xs cursor-pointer"
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

