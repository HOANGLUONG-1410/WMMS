import { useInventoryStockDetail } from "../hooks/useInventory";
import type { InventoryStockDto } from "../types";

interface StockDetailModalProps {
  isOpen: boolean;
  stockItem: InventoryStockDto | null;
  onClose: () => void;
  onOpenFullLedgerForMaterial: (materialId: number, materialCode: string) => void;
}

export default function StockDetailModal({
  isOpen,
  stockItem,
  onClose,
  onOpenFullLedgerForMaterial,
}: StockDetailModalProps) {
  const { data: detail, isLoading } = useInventoryStockDetail(
    isOpen && stockItem ? stockItem.materialId : null,
  );

  if (!isOpen || !stockItem) return null;

  const activeData = detail || {
    ...stockItem,
    allocations: [],
    recentTransactions: [],
  };

  const formatQty = (val: number, uom: string) => {
    const upper = (uom || "").toUpperCase();
    const formatted =
      upper === "M3" || upper === "M³" || upper === "M2"
        ? val.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
        : val.toLocaleString("en-US", { maximumFractionDigits: 2 });
    const displayUom = upper === "M3" ? "m³" : upper === "M2" ? "m²" : uom;
    return `${formatted} ${displayUom}`;
  };

  const getTxBadge = (txType: string) => {
    const upper = (txType || "").toUpperCase();
    if (upper === "INITIAL_IMPORT") {
      return {
        label: "Số dư đầu kỳ",
        className: "bg-emerald-50 text-emerald-700 border-emerald-200",
        sign: "+",
      };
    }
    if (upper === "IN_RECEIPT") {
      return {
        label: "Nhập kho PO",
        className: "bg-blue-50 text-blue-700 border-blue-200",
        sign: "+",
      };
    }
    if (upper === "OUT_ISSUE") {
      return {
        label: "Xuất kho sản xuất",
        className: "bg-amber-50 text-amber-800 border-amber-200",
        sign: "-",
      };
    }
    return {
      label: "Điều chỉnh kiểm kê",
      className: "bg-purple-50 text-purple-800 border-purple-200",
      sign: "±",
    };
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-4xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between bg-slate-50">
          <div>
            <div className="flex items-center gap-2">
              <span className="px-2.5 py-0.5 text-xs font-bold uppercase rounded-md bg-blue-100 text-blue-800 border border-blue-200">
                Chi tiết tồn kho
              </span>
              <h2 className="text-base font-bold text-slate-900">
                Tra cứu giữ chỗ tồn kho &amp; sổ kho vật tư
              </h2>
            </div>
            <p className="text-sm text-slate-600 mt-1">
              Vật tư:{" "}
              <strong className="font-mono text-slate-900">{activeData.materialCode}</strong> —{" "}
              {activeData.materialName}{" "}
              {activeData.dimensionSpec ? `(${activeData.dimensionSpec})` : ""}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-lg font-bold leading-none px-2 py-1 rounded-lg hover:bg-slate-200/60 transition"
          >
            ✕
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-5 overflow-y-auto flex-1">
          {/* 3-Tier Mathematical Balance Cards */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="p-4 rounded-xl border border-slate-200 bg-slate-50/70">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                1. TỒN THỰC TẾ (VẬT LÝ)
              </div>
              <div className="text-xl font-extrabold text-slate-900 mt-1.5">
                {formatQty(activeData.physicalQty, activeData.unitOfMeasure)}
              </div>
              <div className="text-xs text-slate-500 mt-1">
                Hàng đang có trên kệ kho
              </div>
            </div>

            <div className="p-4 rounded-xl border border-amber-200 bg-amber-50/60">
              <div className="text-xs font-bold uppercase tracking-wider text-amber-800">
                2. TỒN GIỮ CHỖ (PHÂN BỔ)
              </div>
              <div className="text-xl font-extrabold text-amber-700 mt-1.5">
                − {formatQty(activeData.allocatedQty, activeData.unitOfMeasure)}
              </div>
              <div className="text-xs text-amber-700 mt-1">
                Đã khóa cho các đơn có BOM được duyệt
              </div>
            </div>

            <div
              className={`p-4 rounded-xl border ${
                activeData.availableQty <= 0 || activeData.isBelowSafetyStock
                  ? "border-rose-200 bg-rose-50/60"
                  : "border-emerald-200 bg-emerald-50/60"
              }`}
            >
              <div
                className={`text-xs font-bold uppercase tracking-wider ${
                  activeData.availableQty <= 0 || activeData.isBelowSafetyStock
                    ? "text-rose-800"
                    : "text-emerald-800"
                }`}
              >
                3. TỒN KHẢ DỤNG (TỰ DO)
              </div>
              <div
                className={`text-xl font-extrabold mt-1.5 ${
                  activeData.availableQty <= 0 || activeData.isBelowSafetyStock
                    ? "text-rose-600"
                    : "text-emerald-700"
                }`}
              >
                = {formatQty(activeData.availableQty, activeData.unitOfMeasure)}
              </div>
              <div className="text-xs text-slate-600 mt-1">
                Tồn an toàn: {formatQty(activeData.safetyStockQty, activeData.unitOfMeasure)}
              </div>
            </div>
          </div>

          {isLoading ? (
            <div className="py-8 text-center text-sm text-slate-400">
              Đang tải chi tiết đơn hàng giữ chỗ và sổ kho từ máy chủ...
            </div>
          ) : (
            <>
              {/* Phần 1: Các đơn hàng đang giữ chỗ */}
              <div className="space-y-2.5">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                    Danh sách đơn hàng đang giữ chỗ
                  </h3>
                </div>

                <div className="border border-slate-200 rounded-xl overflow-x-auto">
                  <table className="w-full text-left border-collapse text-sm">
                    <thead>
                      <tr className="bg-[#F8FAFC] border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500">
                        <th className="py-3 px-4">Order Space</th>
                        <th className="py-3 px-4">Số PO</th>
                        <th className="py-3 px-4">Mã SKU sản phẩm</th>
                        <th className="py-3 px-4 text-right">SL cần</th>
                        <th className="py-3 px-4 text-right">Đã giữ chỗ</th>
                        <th className="py-3 px-4">Ngày ETD</th>
                        <th className="py-3 px-4 text-center">Trạng thái</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200">
                      {activeData.allocations && activeData.allocations.length > 0 ? (
                        activeData.allocations.map((alloc) => (
                          <tr key={alloc.allocationId} className="hover:bg-slate-50">
                            <td className="py-3.5 px-4 whitespace-nowrap">
                              <span className="px-2.5 py-0.5 rounded-md bg-blue-50 text-blue-700 border border-blue-200 font-mono text-xs font-bold">
                                {alloc.spaceKey}
                              </span>
                            </td>
                            <td className="py-3.5 px-4 font-mono font-semibold text-slate-800 whitespace-nowrap">
                              {alloc.poNumber}
                            </td>
                            <td className="py-3.5 px-4">
                              <div className="font-semibold text-slate-900">{alloc.productSku}</div>
                              <div className="text-xs text-slate-500">{alloc.productName}</div>
                            </td>
                            <td className="py-3.5 px-4 text-right font-medium text-slate-700 whitespace-nowrap">
                              {formatQty(alloc.requiredQty, activeData.unitOfMeasure)}
                            </td>
                            <td className="py-3.5 px-4 text-right font-bold text-amber-700 whitespace-nowrap">
                              {formatQty(alloc.reservedQty, activeData.unitOfMeasure)}
                            </td>
                            <td className="py-3.5 px-4 text-slate-600 whitespace-nowrap">
                              {alloc.etdDate
                                ? new Date(alloc.etdDate).toLocaleDateString("en-GB")
                                : "—"}
                            </td>
                            <td className="py-3.5 px-4 text-center whitespace-nowrap">
                              <span className="px-2.5 py-0.5 text-xs font-bold uppercase rounded-full bg-amber-50 text-amber-800 border border-amber-200">
                                {alloc.allocationStatus || "ĐANG GIỮ CHỖ"}
                              </span>
                            </td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={7} className="py-6 text-center text-slate-500 text-sm">
                            Vật tư này hiện không có đơn hàng nào đang khóa giữ chỗ.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Phần 2: Lịch sử giao dịch sổ kho gần nhất của vật tư này */}
              <div className="space-y-2.5">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                    Lịch sử giao dịch sổ kho gần nhất của {activeData.materialCode}
                  </h3>
                  <button
                    type="button"
                    onClick={() => {
                      onOpenFullLedgerForMaterial(
                        activeData.materialId,
                        activeData.materialCode,
                      );
                      onClose();
                    }}
                    className="text-sm font-semibold text-blue-600 hover:text-blue-700 underline"
                  >
                    Mở trên Sổ kho điện tử →
                  </button>
                </div>

                <div className="border border-slate-200 rounded-xl overflow-x-auto">
                  <table className="w-full text-left border-collapse text-sm">
                    <thead>
                      <tr className="bg-[#F8FAFC] border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500">
                        <th className="py-3 px-4">Mã GD</th>
                        <th className="py-3 px-4">Ngày thực hiện</th>
                        <th className="py-3 px-4">Loại giao dịch</th>
                        <th className="py-3 px-4">Mã phiếu tham chiếu</th>
                        <th className="py-3 px-4 text-right">Số lượng biến động</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200">
                      {activeData.recentTransactions &&
                      activeData.recentTransactions.length > 0 ? (
                        activeData.recentTransactions.map((tx) => {
                          const badge = getTxBadge(tx.txType);
                          return (
                            <tr key={tx.transactionId} className="hover:bg-slate-50">
                              <td className="py-3.5 px-4 font-mono text-slate-600 font-semibold whitespace-nowrap">
                                #TX-{String(tx.transactionId).padStart(4, "0")}
                              </td>
                              <td className="py-3.5 px-4 text-slate-600 whitespace-nowrap">
                                {new Date(tx.createdAt).toLocaleString("en-GB")}
                              </td>
                              <td className="py-3.5 px-4 whitespace-nowrap">
                                <span
                                  className={`px-2.5 py-0.5 text-xs font-bold rounded-full border ${badge.className}`}
                                >
                                  {badge.label}
                                </span>
                              </td>
                              <td className="py-3.5 px-4 font-mono font-semibold text-slate-800 whitespace-nowrap">
                                {tx.referenceCode || "—"}
                              </td>
                              <td className="py-3.5 px-4 text-right font-bold text-emerald-700 whitespace-nowrap">
                                {badge.sign}
                                {formatQty(tx.quantity, tx.unitOfMeasure)}
                              </td>
                            </tr>
                          );
                        })
                      ) : (
                        <tr>
                          <td colSpan={5} className="py-6 text-center text-slate-500 text-sm">
                            Chưa có giao dịch sổ kho nào cho vật tư này.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            </>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-slate-200 bg-slate-50 flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="px-5 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg shadow-2xs transition"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}

