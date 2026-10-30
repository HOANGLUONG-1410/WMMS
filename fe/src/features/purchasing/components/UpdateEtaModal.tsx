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
      `Updated delivery schedule and ETA (${promisedEtaDate}) for ${purchaseOrder.poCode}.`,
    );
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-md overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
          <h2 className="text-sm font-extrabold text-slate-900">
            Track Shipment &amp; Update Vendor ETA
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
          <div className="p-3 bg-slate-50 rounded-lg border border-slate-200 text-xs space-y-1">
            <div>
              <span className="text-slate-400">PO Reference:</span>{" "}
              <strong className="font-mono text-slate-900">{purchaseOrder.poCode}</strong>
            </div>
            <div>
              <span className="text-slate-400">Supplier:</span>{" "}
              <strong className="text-slate-900">{purchaseOrder.supplierName}</strong>
            </div>
            <div>
              <span className="text-slate-400">Items:</span>{" "}
              <span className="text-slate-700">{purchaseOrder.orderItemsSummary}</span>
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Promised ETA Date
            </label>
            <input
              type="date"
              value={promisedEtaDate}
              onChange={(e) => setPromisedEtaDate(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Fulfillment Status
            </label>
            <select
              value={fulfillmentStatus}
              onChange={(e) => setFulfillmentStatus(e.target.value)}
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="SUPPLIER CONFIRMED">SUPPLIER CONFIRMED</option>
              <option value="PO ISSUED / IN TRANSIT">PO ISSUED / IN TRANSIT</option>
              <option value="RECEIVED & COMMITTED">RECEIVED &amp; COMMITTED</option>
            </select>
          </div>

          <div className="pt-2 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition"
            >
              Close
            </button>
            <button
              type="submit"
              disabled={updateEtaMutation.isPending}
              className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition disabled:opacity-60"
            >
              {updateEtaMutation.isPending ? "Saving..." : "Save Schedule Update"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

