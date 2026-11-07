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

  // Màu sắc huy hiệu Fulfillment Status
  const getFulfillmentBadgeClass = (status: string) => {
    const upper = String(status || "").toUpperCase();
    if (upper.includes("IN TRANSIT") || upper.includes("ISSUED")) {
      return "bg-amber-50 text-amber-800 border-amber-200";
    }
    if (upper.includes("CONFIRMED")) {
      return "bg-blue-50 text-blue-700 border-blue-200";
    }
    if (upper.includes("RECEIVED") || upper.includes("COMMITTED")) {
      return "bg-emerald-50 text-emerald-700 border-emerald-200";
    }
    return "bg-slate-100 text-slate-700 border-slate-200";
  };

  const getFulfillmentDotClass = (status: string) => {
    const upper = String(status || "").toUpperCase();
    if (upper.includes("IN TRANSIT") || upper.includes("ISSUED")) {
      return "bg-amber-500";
    }
    if (upper.includes("CONFIRMED")) {
      return "bg-blue-600";
    }
    if (upper.includes("RECEIVED") || upper.includes("COMMITTED")) {
      return "bg-emerald-600";
    }
    return "bg-slate-500";
  };

  return (
    <WmmsAppShell mode="purchasing">
      <div className="max-w-[1400px] mx-auto space-y-6">
        {/* Thông báo phản hồi */}
        {toastBanner && (
          <div
            className={`px-4 py-3 rounded-xl border text-sm font-semibold flex items-center justify-between ${
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

        {/* Header + Generate PO from PR */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
              Quản lý đơn đặt hàng nhà cung cấp
            </h1>
            <p className="text-sm text-slate-500 mt-1">
              Gom các yêu cầu mua đã duyệt theo nhà cung cấp, phát hành đơn đặt hàng chính thức và ghi nhận ngày giao hàng (ETA) mà nhà cung cấp cam kết.
            </p>
          </div>

          <button
            type="button"
            onClick={() => setShowGeneratePoModal(true)}
            className="px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition self-start sm:self-auto inline-flex items-center gap-2 shrink-0"
          >
            <svg className="w-4 h-4 stroke-[2.5]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            <span>Lập PO từ PR</span>
          </button>
        </div>

        {/* Endorsement Alert Banner + Batch Issue POs */}
        <div className="bg-emerald-50/70 border border-emerald-200 rounded-xl px-5 py-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="text-sm text-slate-800">
            <strong className="font-bold text-slate-900">
              Phê duyệt mới từ Chủ xưởng:
            </strong>{" "}
            PR gộp ngày 2026-10-20 đã được phê duyệt. 3 đơn đặt hàng NCC sẵn sàng phát hành.
          </div>

          <button
            type="button"
            onClick={handleBatchIssue}
            disabled={batchIssueMutation.isPending}
            className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg shadow-xs transition self-start sm:self-auto shrink-0 disabled:opacity-60"
          >
            {batchIssueMutation.isPending ? "Đang phát hành..." : "Phát hành PO hàng loạt"}
          </button>
        </div>

        {/* Khối Active Supplier Purchase Orders */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <h2 className="text-base font-bold text-slate-900">
              Đơn đặt hàng NCC đang hoạt động
            </h2>

            {/* Search PO #, supplier... */}
            <div className="relative w-full sm:w-64">
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
                value={searchKeyword}
                onChange={(e) => setSearchKeyword(e.target.value)}
                placeholder="Tìm số PO, nhà cung cấp..."
                className="w-full pl-9 pr-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
              />
            </div>
          </div>

          {/* Active Supplier Purchase Orders Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-6">Mã PO</th>
                  <th className="py-3.5 px-4">Nhà cung cấp</th>
                  <th className="py-3.5 px-4">Nhóm vật tư</th>
                  <th className="py-3.5 px-4">Tóm tắt hàng đặt</th>
                  <th className="py-3.5 px-4">Ngày phát hành</th>
                  <th className="py-3.5 px-4">Ngày giao cam kết (ETA)</th>
                  <th className="py-3.5 px-4">Trạng thái thực hiện</th>
                  <th className="py-3.5 px-6 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm">
                {isLoading && (
                  <tr>
                    <td colSpan={8} className="py-10 text-center text-slate-400 text-sm">
                      Đang tải đơn đặt hàng nhà cung cấp...
                    </td>
                  </tr>
                )}

                {!isLoading && purchaseOrders.length === 0 && (
                  <tr>
                    <td colSpan={8} className="py-10 text-center text-slate-500 text-sm">
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
                        <td className="py-4 px-6 font-mono text-sm font-bold text-slate-900 whitespace-nowrap">
                          {po.poCode}
                        </td>
                        <td className="py-4 px-4 font-semibold text-slate-900 whitespace-nowrap">
                          {po.supplierName}
                        </td>
                        <td className="py-4 px-4 text-slate-600 whitespace-nowrap">
                          {po.materialCategory}
                        </td>
                        <td className="py-4 px-4 text-slate-800">{po.orderItemsSummary}</td>
                        <td className="py-4 px-4 text-slate-600 whitespace-nowrap">{po.issueDate}</td>
                        <td className="py-4 px-4 whitespace-nowrap">
                          {po.etaDaysLabel && !isCompleted ? (
                            <span className="font-bold text-blue-600">
                              {po.promisedEtaDate} ({po.etaDaysLabel})
                            </span>
                          ) : (
                            <span className="text-slate-600">{po.promisedEtaDate}</span>
                          )}
                        </td>
                        <td className="py-4 px-4 whitespace-nowrap">
                          <span
                            className={`inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full border ${getFulfillmentBadgeClass(
                              po.fulfillmentStatus,
                            )}`}
                          >
                            <span
                              className={`w-1.5 h-1.5 rounded-full ${getFulfillmentDotClass(
                                po.fulfillmentStatus,
                              )}`}
                            />
                            {fulfillmentStatusLabel(po.fulfillmentStatus)}
                          </span>
                        </td>
                        <td className="py-4 px-6 text-right whitespace-nowrap">
                          {isCompleted ? (
                            <span className="inline-block px-3.5 py-1.5 bg-slate-50 text-slate-400 border border-slate-200 text-xs font-semibold rounded-lg select-none">
                              Hoàn tất
                            </span>
                          ) : isConfirmed ? (
                            <button
                              type="button"
                              onClick={() => setSelectedPoForEta(po)}
                              className="px-3.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg shadow-2xs transition"
                            >
                              Cập nhật ETA
                            </button>
                          ) : (
                            <button
                              type="button"
                              onClick={() => setSelectedPoForEta(po)}
                              className="px-3.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg shadow-2xs transition"
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
