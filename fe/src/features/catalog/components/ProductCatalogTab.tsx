import { useState, useMemo } from "react";
import { useVisualProducts } from "../hooks/useCatalog";
import type { VisualCatalogProductDto } from "../types";
import RoutingDetailModal from "./RoutingDetailModal";

const CATEGORIES = [
  { key: "ALL", label: "Tất cả nhóm", icon: "🪑" },
  { key: "CHAIR", label: "Ghế", icon: "💺" },
  { key: "TABLE", label: "Bàn", icon: "🪵" },
  { key: "CABINET", label: "Tủ", icon: "🗄️" },
  { key: "BED", label: "Giường", icon: "🛏️" },
  { key: "SHELF", label: "Kệ", icon: "📚" },
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
        <div className="flex flex-wrap items-center gap-2 w-full md:w-auto">
          {CATEGORIES.map((cat) => {
            const isActive = selectedCategory === cat.key;
            return (
              <button
                key={cat.key}
                type="button"
                onClick={() => setSelectedCategory(cat.key)}
                className={`px-3.5 py-2 text-sm font-semibold rounded-lg transition flex items-center gap-1.5 ${
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
          <svg
            className="w-4 h-4 text-slate-400 absolute left-3 top-2.5"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            viewBox="0 0 24 24"
          >
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Tìm theo SKU, tên, loại gỗ..."
            className="w-full pl-9 pr-8 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
          />
          {searchTerm && (
            <button
              type="button"
              onClick={() => setSearchTerm("")}
              className="absolute right-2.5 top-2 text-sm text-slate-400 hover:text-slate-600"
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
          <p className="text-sm font-medium">Đang tải danh mục sản phẩm trực quan từ máy chủ...</p>
        </div>
      )}

      {/* Error state */}
      {isError && (
        <div className="bg-rose-50 border border-rose-200 rounded-xl p-6 text-center text-rose-700 text-sm font-medium">
          {(error as { message?: string })?.message || "Không tải được danh mục sản phẩm."}
        </div>
      )}

      {/* Empty state */}
      {!isLoading && !isError && filteredProducts.length === 0 && (
        <div className="bg-white rounded-xl border border-slate-200 p-12 text-center text-slate-400 shadow-xs">
          <div className="text-3xl mb-2">🪵</div>
          <p className="text-base font-bold text-slate-700">Không tìm thấy sản phẩm chuẩn</p>
          <p className="text-sm text-slate-500 mt-1">
            Hãy đổi nhóm sản phẩm hoặc xóa từ khóa tìm kiếm.
          </p>
        </div>
      )}

      {/* Visual Products Grid */}
      {!isLoading && !isError && filteredProducts.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
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
                    <span className="px-2.5 py-0.5 text-xs font-bold uppercase rounded-md bg-slate-200/90 text-slate-700">
                      {prod.categoryName}
                    </span>

                    {prod.hasActiveBom ? (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 text-xs font-bold rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">
                        <span className="w-1.5 h-1.5 rounded-full bg-emerald-600" />
                        BOM đang dùng
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 text-xs font-semibold rounded-full bg-amber-50 text-amber-700 border border-amber-200">
                        <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                        Quy trình mặc định
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
                    <span className="font-mono text-xs font-bold text-slate-700 mt-2 bg-white px-2.5 py-0.5 rounded border border-slate-200 shadow-2xs">
                      {prod.productSku}
                    </span>
                  </div>
                </div>

                {/* Product Meta Body */}
                <div className="p-5 space-y-3">
                  <h4 className="text-base font-bold text-slate-900 line-clamp-1 leading-snug">
                    {prod.productName}
                  </h4>

                  <div className="space-y-2 text-sm text-slate-600">
                    <div className="flex items-center justify-between">
                      <span className="text-slate-500">Kích thước:</span>
                      <span className="font-semibold text-slate-800 font-mono">
                        {prod.dimensionsCm || "N/A"}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-500">Loại gỗ:</span>
                      <span className="font-semibold text-slate-800">
                        {prod.woodSpecies || "Gỗ cứng tiêu chuẩn"}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-500">Hoàn thiện / Màu:</span>
                      <span className="font-semibold text-slate-800">
                        {prod.finishColor || "PU trong tự nhiên"}
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="p-5 pt-0">
                <button
                  type="button"
                  onClick={() => setInspectingProduct(prod)}
                  className="w-full py-2.5 px-3 bg-white hover:bg-blue-50 text-slate-700 hover:text-blue-700 font-semibold text-sm rounded-lg border border-slate-300 hover:border-blue-300 transition flex items-center justify-center gap-2 shadow-2xs"
                >
                  <span>Xem các công đoạn chuẩn</span>
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

