import { useState } from "react";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import ProductCatalogTab from "../components/ProductCatalogTab";
import MaterialCatalogTab from "../components/MaterialCatalogTab";
import { useVisualProducts, useMaterials } from "../hooks/useCatalog";
import { useSession } from "../../auth/store/SessionContext";

type ActiveTab = "products" | "materials";

export default function CatalogPage() {
  const [activeTab, setActiveTab] = useState<ActiveTab>("products");
  const { currentUser } = useSession();

  // Fetch dữ liệu tóm tắt cho stats bar
  const { data: products } = useVisualProducts();
  const { data: materials } = useMaterials();

  const totalProducts = products?.length ?? 0;
  const activeBomProducts = products?.filter((p) => p.hasActiveBom).length ?? 0;
  const totalMaterials = materials?.length ?? 0;
  const fscMaterialsCount = materials?.filter((m) => m.fscCertified).length ?? 0;

  // Quyết định mode cho WmmsAppShell dựa trên role của user
  const shellMode =
    currentUser?.activeRole?.toUpperCase() === "PURCHASING"
      ? "purchasing"
      : currentUser?.activeRole?.toUpperCase() === "ADMIN"
      ? "admin"
      : "owner";

  return (
    <WmmsAppShell mode={shellMode}>
      <div className="max-w-[1400px] mx-auto space-y-6">
        {/* Page Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
              Danh mục sản phẩm &amp; vật tư gốc
            </h1>
            <p className="text-sm text-slate-500 mt-1">
              Kho dữ liệu kỹ thuật tập trung cho thành phẩm chuẩn, các công đoạn sản xuất và vật tư thô của xưởng.
            </p>
          </div>
        </div>

        {/* Quick KPI Stat Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
          <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">SKU chuẩn</span>
              <span className="p-2 rounded-lg bg-blue-50 text-blue-600">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">{totalProducts}</div>
            <span className="text-xs text-slate-500 mt-1 block">Sản phẩm trong thư viện trực quan</span>
          </div>

          <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">BOM đang hoạt động</span>
              <span className="p-2 rounded-lg bg-emerald-50 text-emerald-600">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M9 11l3 3L22 4" /><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-emerald-700 mt-2">{activeBomProducts}</div>
            <span className="text-xs text-slate-500 mt-1 block">Mẫu sẵn sàng sản xuất</span>
          </div>

          <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">Mã vật tư thô</span>
              <span className="p-2 rounded-lg bg-amber-50 text-amber-600">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M20 7h-9" /><path d="M14 17H5" /><circle cx="17" cy="17" r="3" /><circle cx="7" cy="7" r="3" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">{totalMaterials}</div>
            <span className="text-xs text-slate-500 mt-1 block">Gỗ, phụ kiện, sơn &amp; bao bì</span>
          </div>

          <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">Chứng nhận FSC 100%</span>
              <span className="p-2 rounded-lg bg-emerald-50 text-emerald-700">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-emerald-700 mt-2">{fscMaterialsCount}</div>
            <span className="text-xs text-slate-500 mt-1 block">Vật tư có chứng nhận sinh thái</span>
          </div>
        </div>

        {/* Tab Navigation Switcher */}
        <div className="flex border-b border-slate-200 gap-2 bg-white px-5 pt-2 rounded-xl border shadow-xs">
          <button
            type="button"
            onClick={() => setActiveTab("products")}
            className={`px-4 py-3 text-sm font-bold transition flex items-center gap-2 border-b-2 -mb-px ${
              activeTab === "products"
                ? "border-blue-600 text-blue-700 bg-blue-50/40 rounded-t-lg"
                : "border-transparent text-slate-500 hover:text-slate-800"
            }`}
          >
            <span>Danh mục thành phẩm ({totalProducts})</span>
          </button>

          <button
            type="button"
            onClick={() => setActiveTab("materials")}
            className={`px-4 py-3 text-sm font-bold transition flex items-center gap-2 border-b-2 -mb-px ${
              activeTab === "materials"
                ? "border-blue-600 text-blue-700 bg-blue-50/40 rounded-t-lg"
                : "border-transparent text-slate-500 hover:text-slate-800"
            }`}
          >
            <span>Danh mục vật tư thô ({totalMaterials})</span>
          </button>
        </div>

        {/* Tab Content Panels */}
        <div>
          {activeTab === "products" ? <ProductCatalogTab /> : <MaterialCatalogTab />}
        </div>
      </div>
    </WmmsAppShell>
  );
}
