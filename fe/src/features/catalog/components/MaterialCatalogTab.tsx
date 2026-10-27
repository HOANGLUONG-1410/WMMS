import { useState, useMemo } from "react";
import { useMaterials } from "../hooks/useCatalog";

const MATERIAL_GROUPS = [
  { key: "ALL", label: "All Groups", icon: "📦" },
  { key: "LUMBER", label: "Lumber & Plywood (Gỗ)", icon: "🪵" },
  { key: "HARDWARE", label: "Hardware & Fittings (Kim khí)", icon: "🔩" },
  { key: "FINISH_PAINT", label: "Finishing & Paint (Sơn/Hóa chất)", icon: "🎨" },
  { key: "PACKAGING", label: "Packaging & Protection (Bao bì)", icon: "📦" },
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
        <div className="flex flex-wrap items-center gap-1.5 w-full md:w-auto">
          {MATERIAL_GROUPS.map((grp) => {
            const isActive = selectedGroup === grp.key;
            return (
              <button
                key={grp.key}
                type="button"
                onClick={() => setSelectedGroup(grp.key)}
                className={`px-3 py-1.5 text-xs font-semibold rounded-lg transition flex items-center gap-1.5 ${
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
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Search material code, name, specs..."
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

      {/* Table Container */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-200 text-[11px] font-bold text-slate-500 bg-slate-50/80 uppercase tracking-wider">
                <th className="py-3 px-4 w-44">Material Code</th>
                <th className="py-3 px-4">Material Name &amp; Description</th>
                <th className="py-3 px-4 w-32">Group</th>
                <th className="py-3 px-4 w-48">Dimension / Spec</th>
                <th className="py-3 px-3 w-20 text-center">UoM</th>
                <th className="py-3 px-4 w-36 text-center">FSC Certification</th>
                <th className="py-3 px-4 w-28 text-right">Safety Stock</th>
                <th className="py-3 px-4 w-28 text-right">Physical Stock</th>
                <th className="py-3 px-4 w-32 text-center">Stock Health</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 text-xs">
              {isLoading && (
                <tr>
                  <td colSpan={9} className="py-12 text-center text-slate-400">
                    <div className="inline-block w-6 h-6 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                    <p className="text-xs">Loading material catalog...</p>
                  </td>
                </tr>
              )}

              {isError && (
                <tr>
                  <td colSpan={9} className="py-8 text-center text-rose-600 font-medium">
                    {(error as { message?: string })?.message || "Failed to load materials."}
                  </td>
                </tr>
              )}

              {!isLoading && !isError && filteredMaterials.length === 0 && (
                <tr>
                  <td colSpan={9} className="py-8 text-center text-slate-400 italic">
                    No material records found matching your filters.
                  </td>
                </tr>
              )}

              {!isLoading &&
                !isError &&
                filteredMaterials.map((mat) => {
                  const isLowStock = mat.currentStockQty < mat.safetyStockQty;
                  const isOutOfStock = mat.currentStockQty <= 0;

                  return (
                    <tr key={mat.materialId} className="hover:bg-slate-50/70 transition-colors">
                      {/* Material Code */}
                      <td className="py-3.5 px-4 font-mono text-[11px] font-bold text-blue-700">
                        {mat.materialCode}
                      </td>

                      {/* Material Name */}
                      <td className="py-3.5 px-4">
                        <div className="font-semibold text-slate-900">{mat.materialName}</div>
                      </td>

                      {/* Group */}
                      <td className="py-3.5 px-4">
                        <span
                          className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded border ${getGroupBadge(
                            mat.materialGroup,
                          )}`}
                        >
                          {mat.materialGroup}
                        </span>
                      </td>

                      {/* Dimension / Spec */}
                      <td className="py-3.5 px-4 text-slate-600 text-[11px]">
                        {mat.dimensionSpec || "—"}
                      </td>

                      {/* UoM */}
                      <td className="py-3.5 px-3 text-center font-medium text-slate-700">
                        {mat.unitOfMeasure}
                      </td>

                      {/* FSC Certification */}
                      <td className="py-3.5 px-4 text-center">
                        {mat.fscCertified ? (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-extrabold uppercase rounded bg-emerald-100 text-emerald-800 border border-emerald-200">
                            <span>🌿</span> FSC 100%
                          </span>
                        ) : (
                          <span className="inline-block px-2 py-0.5 text-[10px] font-medium text-slate-400 bg-slate-100 rounded">
                            Non-FSC
                          </span>
                        )}
                      </td>

                      {/* Safety Stock */}
                      <td className="py-3.5 px-4 text-right font-mono text-[11px] text-slate-500 font-semibold">
                        {mat.safetyStockQty.toLocaleString()}
                      </td>

                      {/* Physical Stock */}
                      <td className="py-3.5 px-4 text-right font-mono text-[11px] font-bold text-slate-800">
                        {mat.currentStockQty.toLocaleString()}
                      </td>

                      {/* Stock Health */}
                      <td className="py-3.5 px-4 text-center">
                        {isOutOfStock ? (
                          <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-rose-100 text-rose-700 border border-rose-200">
                            Out of Stock
                          </span>
                        ) : isLowStock ? (
                          <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-amber-100 text-amber-800 border border-amber-200">
                            Low Stock
                          </span>
                        ) : (
                          <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-emerald-50 text-emerald-700 border border-emerald-200">
                            Optimal
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
        <div className="px-5 py-3 border-t border-slate-200 bg-slate-50/70 text-xs text-slate-500 flex items-center justify-between">
          <span>
            Total standard items:{" "}
            <span className="font-bold text-slate-800">{filteredMaterials.length}</span>
          </span>
          <span className="text-[11px]">
            Stock Health rule: Flagged as <span className="text-amber-700 font-semibold">Low Stock</span> when{" "}
            <code className="text-slate-700 font-mono">Current &lt; SafetyStockQty</code>
          </span>
        </div>
      </div>
    </div>
  );
}

