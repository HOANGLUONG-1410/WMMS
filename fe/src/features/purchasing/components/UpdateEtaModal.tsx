import { useEffect, useState } from "react";
import { useUpdatePoEta } from "../hooks/usePurchaseOrders";
import type { PoFulfillmentStatus, SupplierPurchaseOrderDto } from "../types";

interface UpdateEtaModalProps {
  purchaseOrder: SupplierPurchaseOrderDto | null;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

export default function UpdateEtaModal({
  purchaseOrder,
  onClose,
  onSuccess,
}: UpdateEtaModalProps) {
  const updateEtaMutation = useUpdatePoEta();
  const [promisedEtaDate, setPromisedEtaDate] = useState("");
  const [fulfillmentStatus, setFulfillmentStatus] =
    useState<PoFulfillmentStatus>("PO ISSUED / IN TRANSIT");

  useEffect(() => {
    if (purchaseOrder) {
      setPromisedEtaDate(purchaseOrder.promisedEtaDate);
      setFulfillmentStatus(purchaseOrder.fulfillmentStatus);
    }
  }, [purchaseOrder]);

  if (!purchaseOrder) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!promisedEtaDate) return;

    await updateEtaMutation.mutateAsync({
      poId: purchaseOrder.poId,
      promisedEtaDate,
      fulfillmentStatus,
    });

    onSuccess(
      `Đã cập nhật lịch giao hàng và ETA (${promisedEtaDate}) cho ${purchaseOrder.poCode}.`,
    );
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-md overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
          <h2 className="text-base font-bold text-slate-900">
            Theo dõi lô hàng &amp; cập nhật ETA của NCC
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-lg font-bold leading-none px-2 py-1 rounded-lg hover:bg-slate-200/60 transition"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="p-4 bg-slate-50 rounded-lg border border-slate-200 text-sm space-y-1.5">
            <div>
              <span className="text-slate-500">Mã PO:</span>{" "}
              <strong className="font-mono text-slate-900">{purchaseOrder.poCode}</strong>
            </div>
            <div>
              <span className="text-slate-500">Nhà cung cấp:</span>{" "}
              <strong className="text-slate-900">{purchaseOrder.supplierName}</strong>
            </div>
            <div>
              <span className="text-slate-500">Hàng đặt:</span>{" "}
              <span className="text-slate-800">{purchaseOrder.orderItemsSummary}</span>
            </div>
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1.5">
              Ngày giao cam kết (ETA)
            </label>
            <input
              type="date"
              value={promisedEtaDate}
              onChange={(e) => setPromisedEtaDate(e.target.value)}
              className="w-full px-3.5 py-2.5 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1.5">
              Trạng thái thực hiện
            </label>
            <select
              value={fulfillmentStatus}
              onChange={(e) => setFulfillmentStatus(e.target.value)}
              className="w-full px-3.5 py-2.5 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="SUPPLIER CONFIRMED">NCC ĐÃ XÁC NHẬN</option>
              <option value="PO ISSUED / IN TRANSIT">ĐÃ PHÁT HÀNH PO / ĐANG VẬN CHUYỂN</option>
              <option value="RECEIVED & COMMITTED">ĐÃ NHẬN &amp; XÁC NHẬN NHẬP KHO</option>
            </select>
          </div>

          <div className="pt-2 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg transition"
            >
              Đóng
            </button>
            <button
              type="submit"
              disabled={updateEtaMutation.isPending}
              className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-60"
            >
              {updateEtaMutation.isPending ? "Đang lưu..." : "Lưu cập nhật lịch giao"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

