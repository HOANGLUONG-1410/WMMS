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
      <div className="max-w-[1400px] mx-auto space-y-5">
        {/* Page Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-2">
              <span className="text-xl">🪵</span>
              <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
                Master Product &amp; Material Catalog
              </h1>
            </div>
            <p className="text-xs text-slate-500 mt-0.5">
              Centralized engineering repository for standardized finished goods, routing stages, and factory raw materials.
            </p>
          </div>

          {/* Role Access Indicator Badge */}
          <div className="flex items-center gap-2 self-start sm:self-auto bg-white px-3 py-1.5 rounded-lg border border-slate-200 shadow-2xs text-xs">
            <span className="text-slate-400 font-medium">Access Scope:</span>
            <span className="font-bold text-slate-800 uppercase">
              {currentUser?.activeRole || "OPERATIONAL"}
            </span>
          </div>
        </div>

        {/* Quick KPI Stat Cards */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <div className="bg-white p-3.5 rounded-xl border border-slate-200 shadow-2xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">Standard SKUs</span>
              <span className="text-base">🪑</span>
            </div>
            <div className="text-xl font-bold text-slate-900 mt-1">{totalProducts}</div>
            <span className="text-[10px] text-slate-400">Products in visual library</span>
          </div>

          <div className="bg-white p-3.5 rounded-xl border border-slate-200 shadow-2xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">Active BOMs</span>
              <span className="text-base">📋</span>
            </div>
            <div className="text-xl font-bold text-emerald-600 mt-1">{activeBomProducts}</div>
            <span className="text-[10px] text-emerald-600 font-medium">Production ready models</span>
          </div>

          <div className="bg-white p-3.5 rounded-xl border border-slate-200 shadow-2xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">Raw Material Items</span>
              <span className="text-base">📦</span>
            </div>
            <div className="text-xl font-bold text-slate-900 mt-1">{totalMaterials}</div>
            <span className="text-[10px] text-slate-400">Lumber, HW, Paint &amp; PKG</span>
          </div>

          <div className="bg-white p-3.5 rounded-xl border border-slate-200 shadow-2xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">FSC 100% Certified</span>
              <span className="text-base">🌿</span>
            </div>
            <div className="text-xl font-bold text-emerald-700 mt-1">{fscMaterialsCount}</div>
            <span className="text-[10px] text-emerald-700 font-medium">Eco certified supplies</span>
          </div>
        </div>

        {/* Tab Navigation Switcher */}
        <div className="flex border-b border-slate-200 gap-1 bg-white px-4 pt-2 rounded-t-xl border-t border-x shadow-2xs">
          <button
            type="button"
            onClick={() => setActiveTab("products")}
            className={`px-4 py-2.5 text-xs font-bold transition flex items-center gap-2 border-b-2 -mb-px ${
              activeTab === "products"
                ? "border-blue-600 text-blue-600 bg-blue-50/50 rounded-t-lg"
                : "border-transparent text-slate-500 hover:text-slate-800"
            }`}
          >
            <span>🪑</span>
            <span>Finished Goods Catalog (Visual Product Library)</span>
            <span className="px-1.5 py-0.2 rounded-full text-[10px] font-bold bg-slate-100 text-slate-600">
              {totalProducts}
            </span>
          </button>

          <button
            type="button"
            onClick={() => setActiveTab("materials")}
            className={`px-4 py-2.5 text-xs font-bold transition flex items-center gap-2 border-b-2 -mb-px ${
              activeTab === "materials"
                ? "border-blue-600 text-blue-600 bg-blue-50/50 rounded-t-lg"
                : "border-transparent text-slate-500 hover:text-slate-800"
            }`}
          >
            <span>🪵</span>
            <span>Raw Material Catalog (Lumber, Hardware, Paint, Packaging)</span>
            <span className="px-1.5 py-0.2 rounded-full text-[10px] font-bold bg-slate-100 text-slate-600">
              {totalMaterials}
            </span>
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
