import { useState } from "react";
import { useCreatePurchaseOrder } from "../hooks/usePurchaseOrders";

interface GeneratePoModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

const APPROVED_PR_VENDOR_GROUPS = [
  {
    supplierName: "Công ty gỗ Hoàng Thông",
    materialCategory: "Gỗ nguyên khối",
    defaultItems: "7,40 m³ gỗ tần bì trắng châu Âu + 20 tấm ván ép Birch",
    defaultEta: "2026-10-30",
  },
  {
    supplierName: "Công ty phụ kiện Việt Đức",
    materialCategory: "Phụ kiện",
    defaultItems: "300 bộ ke góc thép chịu lực + 120 bộ bản lề",
    defaultEta: "2026-10-29",
  },
  {
    supplierName: "Công ty bao bì Duy Tiên",
    materialCategory: "Thùng xuất khẩu",
    defaultItems: "600 thùng carton khung giường (5 lớp, sóng BC)",
    defaultEta: "2026-10-31",
  },
  {
    supplierName: "Oseven Coatings VN",
    materialCategory: "Sơn PU hoàn thiện",
    defaultItems: "150 kg sơn phủ PU 2 thành phần, trong suốt, mờ",
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
      setErrorMsg("Vui lòng nhập nhà cung cấp, hàng đặt và ngày giao cam kết (ETA).");
      return;
    }

    const created = await createPoMutation.mutateAsync({
      supplierName: supplierName.trim(),
      materialCategory: materialCategory.trim(),
      orderItemsSummary: orderItemsSummary.trim(),
      promisedEtaDate,
      sourcePrCode: "PR gộp ngày 2026-10-20",
    });

    onSuccess(
      `Đã phát hành đơn đặt hàng ${created.poCode} tới nhà cung cấp ${created.supplierName} thành công!`,
    );
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
          <h2 className="text-sm font-extrabold text-slate-900">
            Lập đơn đặt hàng NCC từ PR đã duyệt
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
              Nhóm NCC của PR đã duyệt (Daily PR-2026-10-20)
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
                Tên nhà cung cấp
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
                Nhóm vật tư
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
              Tóm tắt hàng đặt (Số lượng đã duyệt)
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
              Ngày NCC cam kết giao (ETA)
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
              Hủy
            </button>
            <button
              type="submit"
              disabled={createPoMutation.isPending}
              className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition disabled:opacity-60"
            >
              {createPoMutation.isPending ? "Đang lập PO..." : "Phát hành PO chính thức"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

