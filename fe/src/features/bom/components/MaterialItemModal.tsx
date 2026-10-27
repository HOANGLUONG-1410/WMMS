import { useEffect, useState } from "react";
import type { BomDetailDto } from "../types";

interface MaterialItemModalProps {
  isOpen: boolean;
  editingItem: BomDetailDto | null;
  onClose: () => void;
  onSave: (item: BomDetailDto) => void;
}

const CATEGORIES = [
  "SOLID LUMBER",
  "SHEET WOOD",
  "HARDWARE",
  "FINISHING",
  "PACKAGING",
];

export default function MaterialItemModal({
  isOpen,
  editingItem,
  onClose,
  onSave,
}: MaterialItemModalProps) {
  const [materialCode, setMaterialCode] = useState("");
  const [materialGroup, setMaterialGroup] = useState("SOLID LUMBER");
  const [materialName, setMaterialName] = useState("");
  const [unitOfMeasure, setUnitOfMeasure] = useState("m³");
  const [netQtyPerUnit, setNetQtyPerUnit] = useState("0.015");
  const [errorMsg, setErrorMsg] = useState("");

  useEffect(() => {
    if (isOpen) {
      if (editingItem) {
        setMaterialCode(editingItem.materialCode);
        setMaterialGroup(editingItem.materialGroup || "SOLID LUMBER");
        setMaterialName(editingItem.materialName);
        setUnitOfMeasure(editingItem.unitOfMeasure);
        setNetQtyPerUnit(String(editingItem.netQtyPerUnit));
      } else {
        setMaterialCode("LUMB-ASH-02");
        setMaterialGroup("SOLID LUMBER");
        setMaterialName("European White Ash Rough Board (30 x 150 x 1200 mm)");
        setUnitOfMeasure("m³");
        setNetQtyPerUnit("0.025");
      }
      setErrorMsg("");
    }
  }, [isOpen, editingItem]);

  if (!isOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const parsedQty = Number(netQtyPerUnit);
    if (!materialCode.trim() || !materialName.trim()) {
      setErrorMsg("Material code and name are required.");
      return;
    }
    if (Number.isNaN(parsedQty) || parsedQty <= 0) {
      setErrorMsg("Net quantity per unit must be greater than 0.");
      return;
    }

    onSave({
      bomDetailId: editingItem?.bomDetailId || Date.now(),
      materialId: editingItem?.materialId || Date.now(),
      materialCode: materialCode.trim().toUpperCase(),
      materialGroup,
      materialName: materialName.trim(),
      unitOfMeasure: unitOfMeasure.trim() || "Pcs",
      netQtyPerUnit: parsedQty,
      grossTotalDemand: editingItem?.grossTotalDemand || 0,
    });
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-md overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
          <h2 className="text-sm font-extrabold text-slate-900">
            {editingItem ? "Edit BOM Material Item" : "Add Raw Material Line Item"}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1.5 py-0.5 rounded"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          {errorMsg && (
            <div className="p-2.5 rounded bg-rose-50 border border-rose-200 text-rose-700 text-xs font-medium">
              {errorMsg}
            </div>
          )}

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Material Code
            </label>
            <input
              type="text"
              value={materialCode}
              onChange={(e) => setMaterialCode(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 font-mono"
              placeholder="e.g. LUMB-OAK-01"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Category
              </label>
              <select
                value={materialGroup}
                onChange={(e) => setMaterialGroup(e.target.value)}
                className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                {CATEGORIES.map((cat) => (
                  <option key={cat} value={cat}>
                    {cat}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Unit of Measure (UoM)
              </label>
              <input
                type="text"
                value={unitOfMeasure}
                onChange={(e) => setUnitOfMeasure(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="m³, Sheet, Pcs, Kg, Box"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Material Name &amp; Dimensions (mm)
            </label>
            <input
              type="text"
              value={materialName}
              onChange={(e) => setMaterialName(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="e.g. American White Oak Rough Board (25 x 120 x 1000 mm)"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Net Quantity per Product Unit
            </label>
            <input
              type="number"
              step="any"
              value={netQtyPerUnit}
              onChange={(e) => setNetQtyPerUnit(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div className="pt-2 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition"
            >
              {editingItem ? "Save Changes" : "Add Material Item"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

