import { useState } from "react";
import { useCreatePurchaseOrder } from "../hooks/usePurchaseOrders";

interface GeneratePoModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

const APPROVED_PR_VENDOR_GROUPS = [
  {
    supplierName: "Hoang Thong Timber Ltd",
    materialCategory: "Solid Lumber",
    defaultItems: "7.40 m³ European White Ash + 20 Sheets Birch Plywood",
    defaultEta: "2026-10-30",
  },
  {
    supplierName: "Viet Duc Fasteners Co",
    materialCategory: "Hardware",
    defaultItems: "300 Sets Heavy-Duty Steel Corner Brackets + 120 Sets Hinges",
    defaultEta: "2026-10-29",
  },
  {
    supplierName: "Duy Tien Packaging Co",
    materialCategory: "Export Boxes",
    defaultItems: "600 Boxes Master Bed Frame Carton (5-Ply BC Flute)",
    defaultEta: "2026-10-31",
  },
  {
    supplierName: "Oseven Coatings VN",
    materialCategory: "Finishing PU",
    defaultItems: "150 Kg Two-Component PU Clear Matt Coating",
    defaultEta: "2026-10-28",
  },
];

export default function GeneratePoModal({
  isOpen,
  onClose,
  onSuccess,
}: GeneratePoModalProps) {
  const createPoMutation = useCreatePurchaseOrder();

  const [selectedVendorIdx, setSelectedVendorIdx] = useState(0);
  const [supplierName, setSupplierName] = useState(APPROVED_PR_VENDOR_GROUPS[0].supplierName);
  const [materialCategory, setMaterialCategory] = useState(
    APPROVED_PR_VENDOR_GROUPS[0].materialCategory,
  );
  const [orderItemsSummary, setOrderItemsSummary] = useState(
    APPROVED_PR_VENDOR_GROUPS[0].defaultItems,
  );
  const [promisedEtaDate, setPromisedEtaDate] = useState(
    APPROVED_PR_VENDOR_GROUPS[0].defaultEta,
  );
  const [errorMsg, setErrorMsg] = useState("");

  if (!isOpen) return null;

  const handleSelectPresetVendor = (idx: number) => {
    const preset = APPROVED_PR_VENDOR_GROUPS[idx];
    setSelectedVendorIdx(idx);
    setSupplierName(preset.supplierName);
    setMaterialCategory(preset.materialCategory);
    setOrderItemsSummary(preset.defaultItems);
    setPromisedEtaDate(preset.defaultEta);
    setErrorMsg("");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!supplierName.trim() || !orderItemsSummary.trim() || !promisedEtaDate) {
      setErrorMsg("Please fill in supplier, requisition items, and promised ETA date.");
      return;
    }

    const created = await createPoMutation.mutateAsync({
      supplierName: supplierName.trim(),
      materialCategory: materialCategory.trim(),
      orderItemsSummary: orderItemsSummary.trim(),
      promisedEtaDate,
      sourcePrCode: "Daily PR-2026-10-20",
    });

    onSuccess(
      `Supplier Purchase Order ${created.poCode} dispatched to vendor ${created.supplierName} successfully!`,
    );
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
          <h2 className="text-sm font-extrabold text-slate-900">
            Generate Supplier Purchase Order from Endorsed PR
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
              Approved PR Vendor Group (Daily PR-2026-10-20)
            </label>
            <select
              value={selectedVendorIdx}
              onChange={(e) => handleSelectPresetVendor(Number(e.target.value))}
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              {APPROVED_PR_VENDOR_GROUPS.map((grp, idx) => (
                <option key={grp.supplierName} value={idx}>
                  {grp.supplierName} — {grp.materialCategory}
                </option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Supplier / Vendor Name
              </label>
              <input
                type="text"
                value={supplierName}
                onChange={(e) => setSupplierName(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Material Category
              </label>
              <input
                type="text"
                value={materialCategory}
                onChange={(e) => setMaterialCategory(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Order Items Summary (Endorsed Quantities)
            </label>
            <input
              type="text"
              value={orderItemsSummary}
              onChange={(e) => setOrderItemsSummary(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Vendor Promised ETA Date
            </label>
            <input
              type="date"
              value={promisedEtaDate}
              onChange={(e) => setPromisedEtaDate(e.target.value)}
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
              disabled={createPoMutation.isPending}
              className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition disabled:opacity-60"
            >
              {createPoMutation.isPending ? "Generating PO..." : "Issue Official Supplier PO"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

