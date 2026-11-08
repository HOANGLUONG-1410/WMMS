import { useState, useMemo } from "react";
import { useMaterials } from "../hooks/useCatalog";

const MATERIAL_GROUPS = [
  { key: "ALL", label: "Tất cả nhóm", icon: "📦" },
  { key: "LUMBER", label: "Gỗ & ván ép", icon: "🪵" },
  { key: "HARDWARE", label: "Phụ kiện kim khí", icon: "🔩" },
  { key: "FINISH_PAINT", label: "Sơn & hóa chất hoàn thiện", icon: "🎨" },
  { key: "PACKAGING", label: "Bao bì & bảo vệ", icon: "📦" },
];

export default function MaterialCatalogTab() {
  const [selectedGroup, setSelectedGroup] = useState("ALL");
  const [searchTerm, setSearchTerm] = useState("");

  const { data: materials, isLoading, isError, error } = useMaterials(selectedGroup);

  // Lọc tìm kiếm client-side
  const filteredMaterials = useMemo(() => {
    if (!materials) return [];
    const term = searchTerm.toLowerCase().trim();
    if (!term) return materials;

    return materials.filter(
      (m) =>
        m.materialCode.toLowerCase().includes(term) ||
        m.materialName.toLowerCase().includes(term) ||
        (m.dimensionSpec && m.dimensionSpec.toLowerCase().includes(term)),
    );
  }, [materials, searchTerm]);

  // Huy hiệu phân nhóm vật tư
  const getGroupBadge = (group: string) => {
    switch (group) {
      case "LUMBER":
        return "bg-amber-100 text-amber-900 border-amber-200";
      case "HARDWARE":
        return "bg-slate-100 text-slate-800 border-slate-300";
      case "FINISH_PAINT":
        return "bg-purple-100 text-purple-800 border-purple-200";
      case "PACKAGING":
        return "bg-blue-100 text-blue-800 border-blue-200";
      default:
        return "bg-slate-100 text-slate-700 border-slate-200";
    }
  };

  return (
    <div className="space-y-4">
      {/* Thanh bộ lọc & Tìm kiếm */}
      <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-xs flex flex-col md:flex-row items-center justify-between gap-3">
        {/* Group Filter Pills */}
        <div className="flex flex-wrap items-center gap-2 w-full md:w-auto">
          {MATERIAL_GROUPS.map((grp) => {
            const isActive = selectedGroup === grp.key;
            return (
              <button
                key={grp.key}
                type="button"
                onClick={() => setSelectedGroup(grp.key)}
                className={`px-3.5 py-2 text-sm font-semibold rounded-lg transition flex items-center gap-1.5 ${
                  isActive
                    ? "bg-blue-600 text-white shadow-xs"
                    : "bg-slate-100 hover:bg-slate-200 text-slate-700"
                }`}
              >
                <span>{grp.icon}</span>
                <span>{grp.label}</span>
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
            placeholder="Tìm theo mã vật tư, tên, quy cách..."
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

      {/* Table Container */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-200 text-xs font-bold text-slate-500 bg-[#F8FAFC] uppercase tracking-wider">
                <th className="py-3.5 px-4 w-44">Mã vật tư</th>
                <th className="py-3.5 px-4">Tên &amp; mô tả vật tư</th>
                <th className="py-3.5 px-4 w-36">Nhóm</th>
                <th className="py-3.5 px-4 w-48">Kích thước / Quy cách</th>
                <th className="py-3.5 px-3 w-20 text-center">ĐVT</th>
                <th className="py-3.5 px-4 w-36 text-center">Chứng nhận FSC</th>
                <th className="py-3.5 px-4 w-28 text-right">Tồn an toàn</th>
                <th className="py-3.5 px-4 w-28 text-right">Tồn vật lý</th>
                <th className="py-3.5 px-4 w-36 text-center">Tình trạng tồn</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 text-sm">
              {isLoading && (
                <tr>
                  <td colSpan={9} className="py-12 text-center text-slate-400">
                    <div className="inline-block w-6 h-6 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                    <p className="text-sm">Đang tải danh mục vật tư...</p>
                  </td>
                </tr>
              )}

              {isError && (
                <tr>
                  <td colSpan={9} className="py-8 text-center text-rose-600 font-medium text-sm">
                    {(error as { message?: string })?.message || "Tải danh mục vật tư thất bại."}
                  </td>
                </tr>
              )}

              {!isLoading && !isError && filteredMaterials.length === 0 && (
                <tr>
                  <td colSpan={9} className="py-8 text-center text-slate-500 text-sm">
                    Không có vật tư nào khớp với bộ lọc.
                  </td>
                </tr>
              )}

              {!isLoading &&
                !isError &&
                filteredMaterials.map((mat) => {
                  const safetyStock = Number(mat.safetyStockQty ?? 0);
                  const currentStock = Number(mat.currentStockQty ?? 0);
                  const isLowStock = currentStock < safetyStock;
                  const isOutOfStock = currentStock <= 0;

                  return (
                    <tr key={mat.materialId} className="hover:bg-slate-50/70 transition-colors">
                      {/* Material Code */}
                      <td className="py-4 px-4 font-mono text-sm font-bold text-blue-700 whitespace-nowrap">
                        {mat.materialCode}
                      </td>

                      {/* Material Name */}
                      <td className="py-4 px-4">
                        <div className="font-semibold text-slate-900">{mat.materialName}</div>
                      </td>

                      {/* Group */}
                      <td className="py-4 px-4">
                        <span
                          className={`inline-block px-2.5 py-0.5 text-xs font-bold uppercase rounded-md border whitespace-nowrap ${getGroupBadge(
                            mat.materialGroup,
                          )}`}
                        >
                          {mat.materialGroup}
                        </span>
                      </td>

                      {/* Dimension / Spec */}
                      <td className="py-4 px-4 text-slate-600 text-sm">
                        {mat.dimensionSpec || "—"}
                      </td>

                      {/* UoM */}
                      <td className="py-4 px-3 text-center font-medium text-slate-700">
                        {mat.unitOfMeasure}
                      </td>

                      {/* FSC Certification */}
                      <td className="py-4 px-4 text-center">
                        {mat.fscCertified ? (
                          <span className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 whitespace-nowrap">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-600" />
                            FSC 100%
                          </span>
                        ) : (
                          <span className="inline-block px-2.5 py-1 text-xs font-medium text-slate-500 bg-slate-100 rounded-full whitespace-nowrap">
                            Không FSC
                          </span>
                        )}
                      </td>

                      {/* Safety Stock */}
                      <td className="py-4 px-4 text-right font-mono text-sm text-slate-500 font-semibold">
                        {safetyStock.toLocaleString()}
                      </td>

                      {/* Physical Stock */}
                      <td className="py-4 px-4 text-right font-mono text-sm font-bold text-slate-800">
                        {currentStock.toLocaleString()}
                      </td>

                      {/* Stock Health */}
                      <td className="py-4 px-4 text-center">
                        {isOutOfStock ? (
                          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-rose-50 text-rose-700 border border-rose-200 whitespace-nowrap">
                            <span className="w-1.5 h-1.5 rounded-full bg-rose-600" />
                            Hết hàng
                          </span>
                        ) : isLowStock ? (
                          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-amber-50 text-amber-800 border border-amber-200 whitespace-nowrap">
                            <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                            Tồn thấp
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 whitespace-nowrap">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-600" />
                            Ổn định
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
            </tbody>
          </table>
        </div>

        {/* Table Footer info */}
        <div className="px-6 py-4 border-t border-slate-200 bg-[#F8FAFC] text-sm text-slate-600 flex items-center justify-between">
          <span>
            Tổng số mã chuẩn:{" "}
            <span className="font-bold text-slate-900">{filteredMaterials.length}</span>
          </span>
          <span className="text-xs text-slate-500">
            Quy tắc tình trạng tồn: gắn cờ <span className="text-amber-700 font-semibold">Tồn thấp</span> khi{" "}
            <code className="text-slate-700 font-mono">Hiện có &lt; Tồn an toàn</code>
          </span>
        </div>
      </div>
    </div>
  );
}

