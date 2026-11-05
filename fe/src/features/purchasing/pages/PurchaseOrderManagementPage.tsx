import { useState } from "react";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import {
  useBatchIssuePurchaseOrders,
  usePurchaseOrders,
} from "../hooks/usePurchaseOrders";
import type { SupplierPurchaseOrderDto } from "../types";
import { fulfillmentStatusLabel } from "../types";
import GeneratePoModal from "../components/GeneratePoModal";
import UpdateEtaModal from "../components/UpdateEtaModal";

export default function PurchaseOrderManagementPage() {
  const [searchKeyword, setSearchKeyword] = useState("");
  const [showGeneratePoModal, setShowGeneratePoModal] = useState(false);
  const [selectedPoForEta, setSelectedPoForEta] =
    useState<SupplierPurchaseOrderDto | null>(null);
  const [toastBanner, setToastBanner] = useState<{
    type: "success" | "error";
    text: string;
  } | null>(null);

  const { data: purchaseOrders = [], isLoading } = usePurchaseOrders({
    searchKeyword: searchKeyword.trim() || undefined,
  });

  const batchIssueMutation = useBatchIssuePurchaseOrders();

  // Field #3: Batch Issue POs
  const handleBatchIssue = async () => {
    await batchIssueMutation.mutateAsync();
    setToastBanner({
      type: "success",
      text: "Tất cả đơn đặt hàng nhà cung cấp đã được duyệt đã được phát hành hàng loạt và gửi tới NCC!",
    });
  };

  // Màu sắc huy hiệu Fulfillment Status giống hệt thiết kế trong Report 3
  const getFulfillmentBadgeClass = (status: string) => {
    const upper = String(status || "").toUpperCase();
    if (upper.includes("IN TRANSIT") || upper.includes("ISSUED")) {
      return "bg-yellow-100 text-amber-800";
    }
    if (upper.includes("CONFIRMED")) {
      return "bg-blue-100 text-blue-700";
    }
    if (upper.includes("RECEIVED") || upper.includes("COMMITTED")) {
      return "bg-emerald-100 text-emerald-700";
    }
    return "bg-slate-100 text-slate-700";
  };

  return (
    <WmmsAppShell
      mode="purchasing"
      customTopLeft={
        <div className="px-3 py-1.5 rounded-md bg-slate-100 border border-slate-200 text-xs font-semibold text-slate-700">
          Theo dõi yêu cầu mua hàng theo nhà cung cấp
        </div>
      }
    >
      <div className="max-w-[1400px] mx-auto space-y-5">
        {/* Thông báo phản hồi */}
        {toastBanner && (
          <div
            className={`px-4 py-3 rounded-lg border text-xs font-semibold flex items-center justify-between ${
              toastBanner.type === "success"
                ? "bg-emerald-50 border-emerald-200 text-emerald-800"
                : "bg-rose-50 border-rose-200 text-rose-700"
            }`}
          >
            <span>{toastBanner.text}</span>
            <button
              type="button"
              onClick={() => setToastBanner(null)}
              className="text-slate-400 hover:text-slate-600 ml-3 font-bold"
            >
              ✕
            </button>
          </div>
        )}

        {/* Header + Field #2: + Generate PO from PR */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
              Quản lý đơn đặt hàng nhà cung cấp
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Gom các yêu cầu mua đã duyệt theo nhà cung cấp, phát hành đơn đặt hàng chính thức và
              ghi nhận ngày giao hàng (ETA) mà nhà cung cấp cam kết.
            </p>
          </div>

          <button
            type="button"
            onClick={() => setShowGeneratePoModal(true)}
            className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition self-start sm:self-auto"
          >
            + Lập PO từ PR
          </button>
        </div>

        {/* Endorsement Alert Banner + Field #3: Batch Issue POs */}
        <div className="bg-emerald-50/70 border border-emerald-200 rounded-lg px-5 py-3.5 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="text-xs text-slate-800">
            <strong className="font-bold text-slate-900">
              Phê duyệt mới từ Chủ xưởng:
            </strong>{" "}
            PR gộp ngày 2026-10-20 đã được phê duyệt. 3 đơn đặt hàng NCC sẵn sàng phát hành.
          </div>

          <button
            type="button"
            onClick={handleBatchIssue}
            disabled={batchIssueMutation.isPending}
            className="px-3.5 py-1.5 bg-[#16a34a] hover:bg-emerald-700 text-white text-xs font-bold rounded-md shadow-2xs transition self-start sm:self-auto shrink-0 disabled:opacity-60"
          >
            {batchIssueMutation.isPending ? "Đang phát hành..." : "Phát hành PO hàng loạt"}
          </button>
        </div>

        {/* Khối Active Supplier Purchase Orders + Fields #4, #5, #6 */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-2xs overflow-hidden">
          <div className="px-5 py-3.5 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <h2 className="text-xs font-bold text-slate-800">
              Đơn đặt hàng NCC đang hoạt động
            </h2>

            {/* Field #4: Search PO #, supplier... */}
            <input
              type="text"
              value={searchKeyword}
              onChange={(e) => setSearchKeyword(e.target.value)}
              placeholder="Tìm số PO, nhà cung cấp..."
              className="w-full sm:w-60 px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #5: Active Supplier Purchase Orders Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-5">Mã PO</th>
                  <th className="py-3 px-4">Nhà cung cấp</th>
                  <th className="py-3 px-4">Nhóm vật tư</th>
                  <th className="py-3 px-4">Tóm tắt hàng đặt</th>
                  <th className="py-3 px-4">Ngày phát hành</th>
                  <th className="py-3 px-4">Ngày giao cam kết (ETA)</th>
                  <th className="py-3 px-4">Trạng thái thực hiện</th>
                  <th className="py-3 px-5 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-xs">
                {isLoading && (
                  <tr>
                    <td colSpan={8} className="py-10 text-center text-slate-400">
                      Đang tải đơn đặt hàng nhà cung cấp...
                    </td>
                  </tr>
                )}

                {!isLoading && purchaseOrders.length === 0 && (
                  <tr>
                    <td colSpan={8} className="py-8 text-center text-slate-400 italic">
                      Không có đơn đặt hàng nào khớp với tìm kiếm.
                    </td>
                  </tr>
                )}

                {!isLoading &&
                  purchaseOrders.map((po) => {
                    const isCompleted =
                      po.fulfillmentStatus.toUpperCase().includes("RECEIVED") ||
                      po.fulfillmentStatus.toUpperCase().includes("COMMITTED");
                    const isConfirmed = po.fulfillmentStatus
                      .toUpperCase()
                      .includes("CONFIRMED");

                    return (
                      <tr
                        key={po.poId}
                        className="hover:bg-slate-50/80 transition-colors"
                      >
                        <td className="py-3.5 px-5 font-mono text-xs font-extrabold text-slate-900">
                          {po.poCode}
                        </td>
                        <td className="py-3.5 px-4 font-semibold text-slate-800">
                          {po.supplierName}
                        </td>
                        <td className="py-3.5 px-4 text-slate-600">{po.materialCategory}</td>
                        <td className="py-3.5 px-4 text-slate-800">{po.orderItemsSummary}</td>
                        <td className="py-3.5 px-4 text-slate-600">{po.issueDate}</td>
                        <td className="py-3.5 px-4">
                          {po.etaDaysLabel && !isCompleted ? (
                            <span className="font-bold text-[#2563eb]">
                              {po.promisedEtaDate} ({po.etaDaysLabel})
                            </span>
                          ) : (
                            <span className="text-slate-600">{po.promisedEtaDate}</span>
                          )}
                        </td>
                        <td className="py-3.5 px-4">
                          <span
                            className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded ${getFulfillmentBadgeClass(
                              po.fulfillmentStatus,
                            )}`}
                          >
                            {fulfillmentStatusLabel(po.fulfillmentStatus)}
                          </span>
                        </td>
                        <td className="py-3.5 px-5 text-right">
                          {/* Field #6: Track Arrival → / Update ETA / Completed */}
                          {isCompleted ? (
                            <span className="inline-block px-3 py-1 bg-slate-50 text-slate-400 border border-slate-200 text-[11px] font-medium rounded select-none">
                              Hoàn tất
                            </span>
                          ) : isConfirmed ? (
                            <button
                              type="button"
                              onClick={() => setSelectedPoForEta(po)}
                              className="px-3 py-1 bg-white hover:bg-slate-100 text-slate-800 border border-slate-300 text-[11px] font-semibold rounded transition"
                            >
                              Cập nhật ETA
                            </button>
                          ) : (
                            <button
                              type="button"
                              onClick={() => setSelectedPoForEta(po)}
                              className="px-3 py-1 bg-white hover:bg-slate-100 text-slate-800 border border-slate-300 text-[11px] font-semibold rounded transition"
                            >
                              Theo dõi hàng về →
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* Modal Generate PO from PR */}
      <GeneratePoModal
        isOpen={showGeneratePoModal}
        onClose={() => setShowGeneratePoModal(false)}
        onSuccess={(msg) => setToastBanner({ type: "success", text: msg })}
      />

      {/* Modal Track Arrival / Update ETA */}
      <UpdateEtaModal
        purchaseOrder={selectedPoForEta}
        onClose={() => setSelectedPoForEta(null)}
        onSuccess={(msg) => setToastBanner({ type: "success", text: msg })}
      />
    </WmmsAppShell>
  );
}
