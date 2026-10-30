import { useState, useMemo } from "react";
import { useVisualProducts } from "../hooks/useCatalog";
import type { VisualCatalogProductDto } from "../types";
import RoutingDetailModal from "./RoutingDetailModal";

const CATEGORIES = [
  { key: "ALL", label: "All Categories", icon: "🪑" },
  { key: "CHAIR", label: "Chairs (Ghế)", icon: "💺" },
  { key: "TABLE", label: "Tables (Bàn)", icon: "🪵" },
  { key: "CABINET", label: "Cabinets (Tủ)", icon: "🗄️" },
  { key: "BED", label: "Beds (Giường)", icon: "🛏️" },
  { key: "SHELF", label: "Shelves (Kệ)", icon: "📚" },
];

export default function ProductCatalogTab() {
  const [selectedCategory, setSelectedCategory] = useState("ALL");
  const [searchTerm, setSearchTerm] = useState("");
  const [inspectingProduct, setInspectingProduct] = useState<VisualCatalogProductDto | null>(null);

  const { data: products, isLoading, isError, error } = useVisualProducts(selectedCategory);

  // Lọc tìm kiếm client-side theo SKU, Tên, hoặc Loại gỗ
  const filteredProducts = useMemo(() => {
    if (!products) return [];
    const term = searchTerm.toLowerCase().trim();
    if (!term) return products;

    return products.filter(
      (p) =>
        p.productSku.toLowerCase().includes(term) ||
        p.productName.toLowerCase().includes(term) ||
        (p.woodSpecies && p.woodSpecies.toLowerCase().includes(term)) ||
        p.categoryName.toLowerCase().includes(term),
    );
  }, [products, searchTerm]);

  return (
    <div className="space-y-4">
      {/* Thanh bộ lọc & Tìm kiếm */}
      <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-xs flex flex-col md:flex-row items-center justify-between gap-3">
        {/* Category Pills */}
        <div className="flex flex-wrap items-center gap-1.5 w-full md:w-auto">
          {CATEGORIES.map((cat) => {
            const isActive = selectedCategory === cat.key;
            return (
              <button
                key={cat.key}
                type="button"
                onClick={() => setSelectedCategory(cat.key)}
                className={`px-3 py-1.5 text-xs font-semibold rounded-lg transition flex items-center gap-1.5 ${
                  isActive
                    ? "bg-blue-600 text-white shadow-xs"
                    : "bg-slate-100 hover:bg-slate-200 text-slate-700"
                }`}
              >
                <span>{cat.icon}</span>
                <span>{cat.label}</span>
              </button>
            );
          })}
        </div>

        {/* Search Bar */}
        <div className="relative w-full md:w-72">
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Search by SKU, name, wood species..."
            className="w-full px-3.5 py-1.5 pl-9 text-xs bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
          />
          <span className="absolute left-3 top-2 text-slate-400 text-xs">🔍</span>
          {searchTerm && (
            <button
              type="button"
              onClick={() => setSearchTerm("")}
              className="absolute right-2.5 top-2 text-xs text-slate-400 hover:text-slate-600"
            >
              ✕
            </button>
          )}
        </div>
      </div>

      {/* Loading state */}
      {isLoading && (
        <div className="bg-white rounded-xl border border-slate-200 p-12 text-center text-slate-400 shadow-xs">
          <div className="inline-block w-7 h-7 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
          <p className="text-xs font-medium">Fetching Visual Product Catalog from backend...</p>
        </div>
      )}

      {/* Error state */}
      {isError && (
        <div className="bg-rose-50 border border-rose-200 rounded-xl p-6 text-center text-rose-700 text-xs font-medium">
          {(error as { message?: string })?.message || "Unable to load visual products."}
        </div>
      )}

      {/* Empty state */}
      {!isLoading && !isError && filteredProducts.length === 0 && (
        <div className="bg-white rounded-xl border border-slate-200 p-12 text-center text-slate-400 shadow-xs">
          <div className="text-3xl mb-2">🪵</div>
          <p className="text-sm font-semibold text-slate-700">No standard products found</p>
          <p className="text-xs text-slate-400 mt-1">
            Try switching category or clearing the search keyword.
          </p>
        </div>
      )}

      {/* Visual Products Grid */}
      {!isLoading && !isError && filteredProducts.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {filteredProducts.map((prod) => (
            <div
              key={prod.productId}
              className="bg-white rounded-xl border border-slate-200 shadow-xs hover:shadow-md transition-all duration-200 overflow-hidden flex flex-col justify-between group"
            >
              {/* Product Header & Preview Box */}
              <div>
                <div className="h-40 bg-gradient-to-br from-slate-50 to-slate-100/80 border-b border-slate-100 relative p-4 flex flex-col justify-between">
                  {/* Top Badges */}
                  <div className="flex items-center justify-between gap-2">
                    <span className="px-2 py-0.5 text-[10px] font-extrabold uppercase rounded bg-slate-200/90 text-slate-700">
                      {prod.categoryName}
                    </span>

                    {prod.hasActiveBom ? (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-bold rounded bg-emerald-100 text-emerald-800 border border-emerald-200">
                        <span className="w-1.5 h-1.5 rounded-full bg-emerald-600" />
                        Active BOM
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-semibold rounded bg-amber-50 text-amber-700 border border-amber-200">
                        Default Routing
                      </span>
                    )}
                  </div>

                  {/* Icon & SKU Big Display */}
                  <div className="flex flex-col items-center justify-center my-auto">
                    <span className="text-4xl group-hover:scale-110 transition-transform duration-200">
                      {prod.categoryName === "CHAIR"
                        ? "💺"
                        : prod.categoryName === "TABLE"
                        ? "🪵"
                        : prod.categoryName === "CABINET"
                        ? "🗄️"
                        : prod.categoryName === "BED"
                        ? "🛏️"
                        : "📦"}
                    </span>
                    <span className="font-mono text-xs font-bold text-slate-600 mt-2 bg-white/80 px-2.5 py-0.5 rounded shadow-2xs">
                      {prod.productSku}
                    </span>
                  </div>
                </div>

                {/* Product Meta Body */}
                <div className="p-4 space-y-2.5">
                  <h4 className="text-sm font-bold text-slate-900 line-clamp-1 leading-snug">
                    {prod.productName}
                  </h4>

                  <div className="space-y-1.5 text-xs text-slate-600">
                    <div className="flex items-center justify-between">
                      <span className="text-slate-400 text-[11px]">Dimensions:</span>
                      <span className="font-semibold text-slate-800 font-mono text-[11px]">
                        {prod.dimensionsCm || "N/A"}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-400 text-[11px]">Wood Species:</span>
                      <span className="font-semibold text-slate-800">
                        {prod.woodSpecies || "Standard Hardwood"}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-400 text-[11px]">Finish / Color:</span>
                      <span className="font-semibold text-slate-800">
                        {prod.finishColor || "Natural PU Clear"}
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="p-4 pt-0">
                <button
                  type="button"
                  onClick={() => setInspectingProduct(prod)}
                  className="w-full py-2 px-3 bg-slate-50 hover:bg-blue-50 text-slate-700 hover:text-blue-700 font-semibold text-xs rounded-lg border border-slate-200 hover:border-blue-200 transition flex items-center justify-center gap-1.5"
                >
                  <span>🔄</span>
                  <span>View Standard Routing Stages</span>
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Modal xem chuỗi công đoạn */}
      <RoutingDetailModal
        product={inspectingProduct}
        onClose={() => setInspectingProduct(null)}
      />
    </div>
  );
}

