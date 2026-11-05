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
                Danh mục sản phẩm &amp; vật tư gốc
              </h1>
            </div>
            <p className="text-xs text-slate-500 mt-0.5">
              Kho dữ liệu kỹ thuật tập trung cho thành phẩm chuẩn, các công đoạn sản xuất và vật tư thô của xưởng.
            </p>
          </div>

          {/* Role Access Indicator Badge */}
          <div className="flex items-center gap-2 self-start sm:self-auto bg-white px-3 py-1.5 rounded-lg border border-slate-200 shadow-2xs text-xs">
            <span className="text-slate-400 font-medium">Phạm vi truy cập:</span>
            <span className="font-bold text-slate-800 uppercase">
              {currentUser?.activeRole || "OPERATIONAL"}
            </span>
          </div>
        </div>

        {/* Quick KPI Stat Cards */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <div className="bg-white p-3.5 rounded-xl border border-slate-200 shadow-2xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">SKU chuẩn</span>
              <span className="text-base">🪑</span>
            </div>
            <div className="text-xl font-bold text-slate-900 mt-1">{totalProducts}</div>
            <span className="text-[10px] text-slate-400">Sản phẩm trong thư viện trực quan</span>
          </div>

          <div className="bg-white p-3.5 rounded-xl border border-slate-200 shadow-2xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">BOM đang hoạt động</span>
              <span className="text-base">📋</span>
            </div>
            <div className="text-xl font-bold text-emerald-600 mt-1">{activeBomProducts}</div>
            <span className="text-[10px] text-emerald-600 font-medium">Mẫu sẵn sàng sản xuất</span>
          </div>

          <div className="bg-white p-3.5 rounded-xl border border-slate-200 shadow-2xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">Mã vật tư thô</span>
              <span className="text-base">📦</span>
            </div>
            <div className="text-xl font-bold text-slate-900 mt-1">{totalMaterials}</div>
            <span className="text-[10px] text-slate-400">Gỗ, phụ kiện, sơn &amp; bao bì</span>
          </div>

          <div className="bg-white p-3.5 rounded-xl border border-slate-200 shadow-2xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500">Chứng nhận FSC 100%</span>
              <span className="text-base">🌿</span>
            </div>
            <div className="text-xl font-bold text-emerald-700 mt-1">{fscMaterialsCount}</div>
            <span className="text-[10px] text-emerald-700 font-medium">Vật tư có chứng nhận sinh thái</span>
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
            <span>Danh mục thành phẩm (Thư viện sản phẩm trực quan)</span>
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
            <span>Danh mục vật tư thô (Gỗ, phụ kiện, sơn, bao bì)</span>
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
