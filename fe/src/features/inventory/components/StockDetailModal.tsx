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
        label: "INITIAL_IMPORT (Số dư đầu kỳ)",
        className: "bg-emerald-100 text-emerald-800 border-emerald-300",
        sign: "+",
      };
    }
    if (upper === "IN_RECEIPT") {
      return {
        label: "IN_RECEIPT (Nhập kho PO)",
        className: "bg-blue-100 text-blue-800 border-blue-300",
        sign: "+",
      };
    }
    if (upper === "OUT_ISSUE") {
      return {
        label: "OUT_ISSUE (Xuất kho SX)",
        className: "bg-amber-100 text-amber-800 border-amber-300",
        sign: "-",
      };
    }
    return {
      label: `${upper} (Điều chỉnh)`,
      className: "bg-purple-100 text-purple-800 border-purple-300",
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
              <span className="px-2 py-0.5 text-[10px] font-extrabold uppercase rounded bg-blue-100 text-blue-800 border border-blue-300">
                CHI TIẾT TỒN KHO
              </span>
              <h2 className="text-sm font-extrabold text-slate-900">
                Tra cứu giữ chỗ tồn kho &amp; sổ kho vật tư
              </h2>
            </div>
            <p className="text-xs text-slate-600 mt-0.5">
              Vật tư:{" "}
              <strong className="font-mono text-slate-900">{activeData.materialCode}</strong> —{" "}
              {activeData.materialName}{" "}
              {activeData.dimensionSpec ? `(${activeData.dimensionSpec})` : ""}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-2 py-1 rounded"
          >
            ✕
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-5 overflow-y-auto flex-1">
          {/* 3-Tier Mathematical Balance Cards */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div className="p-3.5 rounded-lg border border-slate-200 bg-slate-50/70">
              <div className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
                1. TOTAL PHYSICAL STOCK (TỒN THỰC TẾ)
              </div>
              <div className="text-lg font-extrabold text-slate-900 mt-1">
                {formatQty(activeData.physicalQty, activeData.unitOfMeasure)}
              </div>
              <div className="text-[11px] text-slate-500 mt-0.5">
                Hàng đang có trên kệ kho
              </div>
            </div>

            <div className="p-3.5 rounded-lg border border-amber-200 bg-amber-50/60">
              <div className="text-[10px] font-bold uppercase tracking-wider text-amber-800">
                2. ALLOCATED STOCK (TỒN GIỮ CHỖ)
              </div>
              <div className="text-lg font-extrabold text-[#d97706] mt-1">
                − {formatQty(activeData.allocatedQty, activeData.unitOfMeasure)}
              </div>
              <div className="text-[11px] text-amber-700 mt-0.5">
                Đã khóa cho các đơn có BOM được duyệt
              </div>
            </div>

            <div
              className={`p-3.5 rounded-lg border ${
                activeData.availableQty <= 0 || activeData.isBelowSafetyStock
                  ? "border-rose-200 bg-rose-50/60"
                  : "border-emerald-200 bg-emerald-50/60"
              }`}
            >
              <div
                className={`text-[10px] font-bold uppercase tracking-wider ${
                  activeData.availableQty <= 0 || activeData.isBelowSafetyStock
                    ? "text-rose-800"
                    : "text-emerald-800"
                }`}
              >
                3. AVAILABLE STOCK (TỒN KHẢ DỤNG)
              </div>
              <div
                className={`text-lg font-extrabold mt-1 ${
                  activeData.availableQty <= 0 || activeData.isBelowSafetyStock
                    ? "text-rose-600"
                    : "text-emerald-600"
                }`}
              >
                = {formatQty(activeData.availableQty, activeData.unitOfMeasure)}
              </div>
              <div className="text-[11px] text-slate-600 mt-0.5">
                Tồn an toàn: {formatQty(activeData.safetyStockQty, activeData.unitOfMeasure)}
              </div>
            </div>
          </div>

          {isLoading ? (
            <div className="py-8 text-center text-xs text-slate-400">
              Đang tải chi tiết đơn hàng giữ chỗ và sổ kho từ máy chủ...
            </div>
          ) : (
            <>
              {/* Phần 1: Các đơn hàng đang giữ chỗ (Active Allocations) */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-extrabold text-slate-800 uppercase tracking-wide">
                    🔒 Danh sách Đơn hàng đang Giữ chỗ (Active Order Space Reservations)
                  </h3>
                </div>

                <div className="border border-slate-200 rounded-lg overflow-x-auto">
                  <table className="w-full text-left border-collapse text-xs">
                    <thead>
                      <tr className="bg-slate-50 border-b border-slate-200 text-[11px] font-semibold text-slate-500">
                        <th className="py-2.5 px-3">Order Space</th>
                        <th className="py-2.5 px-3">Số PO</th>
                        <th className="py-2.5 px-3">Mã SKU sản phẩm</th>
                        <th className="py-2.5 px-3 text-right">SL cần</th>
                        <th className="py-2.5 px-3 text-right">Đã giữ chỗ (Phân bổ)</th>
                        <th className="py-2.5 px-3">Ngày ETD</th>
                        <th className="py-2.5 px-3 text-center">Trạng thái</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200">
                      {activeData.allocations && activeData.allocations.length > 0 ? (
                        activeData.allocations.map((alloc) => (
                          <tr key={alloc.allocationId} className="hover:bg-slate-50">
                            <td className="py-2.5 px-3">
                              <span className="px-2 py-0.5 rounded bg-blue-50 text-blue-700 border border-blue-200 font-mono text-[11px] font-bold">
                                {alloc.spaceKey}
                              </span>
                            </td>
                            <td className="py-2.5 px-3 font-mono font-semibold text-slate-800">
                              {alloc.poNumber}
                            </td>
                            <td className="py-2.5 px-3">
                              <div className="font-semibold text-slate-800">{alloc.productSku}</div>
                              <div className="text-[11px] text-slate-500">{alloc.productName}</div>
                            </td>
                            <td className="py-2.5 px-3 text-right font-medium text-slate-700">
                              {formatQty(alloc.requiredQty, activeData.unitOfMeasure)}
                            </td>
                            <td className="py-2.5 px-3 text-right font-bold text-[#d97706]">
                              {formatQty(alloc.reservedQty, activeData.unitOfMeasure)}
                            </td>
                            <td className="py-2.5 px-3 text-slate-600">
                              {alloc.etdDate
                                ? new Date(alloc.etdDate).toLocaleDateString("en-GB")
                                : "—"}
                            </td>
                            <td className="py-2.5 px-3 text-center">
                              <span className="px-2 py-0.5 text-[10px] font-extrabold uppercase rounded bg-amber-100 text-amber-800 border border-amber-300">
                                {alloc.allocationStatus || "RESERVED"}
                              </span>
                            </td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={7} className="py-5 text-center text-slate-400">
                            Vật tư này hiện không có đơn hàng nào đang khóa giữ chỗ (Allocated = 0).
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Phần 2: Lịch sử giao dịch sổ kho gần nhất của vật tư này */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-extrabold text-slate-800 uppercase tracking-wide">
                    📒 Lịch sử Giao dịch Sổ kho gần nhất của {activeData.materialCode}
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
                    className="text-xs font-bold text-blue-600 hover:text-blue-800 underline"
                  >
                    Mở trên Tab Sổ kho điện tử (Stock Ledger) →
                  </button>
                </div>

                <div className="border border-slate-200 rounded-lg overflow-x-auto">
                  <table className="w-full text-left border-collapse text-xs">
                    <thead>
                      <tr className="bg-slate-50 border-b border-slate-200 text-[11px] font-semibold text-slate-500">
                        <th className="py-2.5 px-3">Mã GD</th>
                        <th className="py-2.5 px-3">Ngày thực hiện</th>
                        <th className="py-2.5 px-3">Loại giao dịch</th>
                        <th className="py-2.5 px-3">Mã phiếu tham chiếu</th>
                        <th className="py-2.5 px-3 text-right">Số lượng biến động</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-200">
                      {activeData.recentTransactions &&
                      activeData.recentTransactions.length > 0 ? (
                        activeData.recentTransactions.map((tx) => {
                          const badge = getTxBadge(tx.txType);
                          return (
                            <tr key={tx.transactionId} className="hover:bg-slate-50">
                              <td className="py-2.5 px-3 font-mono text-slate-500 font-semibold">
                                #TX-{String(tx.transactionId).padStart(4, "0")}
                              </td>
                              <td className="py-2.5 px-3 text-slate-600">
                                {new Date(tx.createdAt).toLocaleString("en-GB")}
                              </td>
                              <td className="py-2.5 px-3">
                                <span
                                  className={`px-2 py-0.5 text-[10px] font-bold rounded border ${badge.className}`}
                                >
                                  {badge.label}
                                </span>
                              </td>
                              <td className="py-2.5 px-3 font-mono font-semibold text-slate-800">
                                {tx.referenceCode || "—"}
                              </td>
                              <td className="py-2.5 px-3 text-right font-bold text-emerald-700">
                                {badge.sign}
                                {formatQty(tx.quantity, tx.unitOfMeasure)}
                              </td>
                            </tr>
                          );
                        })
                      ) : (
                        <tr>
                          <td colSpan={5} className="py-5 text-center text-slate-400">
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
        <div className="px-6 py-3.5 border-t border-slate-200 bg-slate-50 flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-1.5 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}

