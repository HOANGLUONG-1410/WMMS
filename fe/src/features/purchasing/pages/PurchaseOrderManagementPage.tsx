import { useState } from "react";
import { useSession } from "../../auth/store/SessionContext";
import { ROLES } from "../../../constants/roles";
import {
  useApprovedRequisitions,
  useCancelPurchaseOrder,
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
  const [selectedPoForCancel, setSelectedPoForCancel] =
    useState<SupplierPurchaseOrderDto | null>(null);
  const [cancelReason, setCancelReason] = useState("");
  const [cancelError, setCancelError] = useState("");
  const [toastBanner, setToastBanner] = useState<{
    type: "success" | "error";
    text: string;
  } | null>(null);

  const { currentUser } = useSession();
  const isOwner = currentUser?.activeRole === ROLES.OWNER;
  const canManagePo =
    currentUser?.activeRole === ROLES.ADMIN ||
    currentUser?.activeRole === ROLES.PURCHASING;

  const { data: purchaseOrders = [], isLoading } = usePurchaseOrders({
    searchKeyword: searchKeyword.trim() || undefined,
  });
  const { data: approvedRequisitions = [] } = useApprovedRequisitions();
  const cancelPoMutation = useCancelPurchaseOrder();

  const handleConfirmCancelPo = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPoForCancel) return;
    setCancelError("");
    try {
      const res = await cancelPoMutation.mutateAsync({
        poId: selectedPoForCancel.poId,
        reason: cancelReason.trim() || undefined,
      });
      setToastBanner({
        type: "success",
        text: res.message || `Đã hủy đơn đặt hàng ${selectedPoForCancel.poCode} thành công.`,
      });
      setSelectedPoForCancel(null);
      setCancelReason("");
    } catch (err: unknown) {
      const msg =
        (err as { message?: string; response?: { data?: { message?: string } } })
          ?.response?.data?.message ||
        (err as { message?: string })?.message ||
        "Không thể hủy đơn đặt hàng này.";
      setCancelError(msg);
    }
  };

  const pendingMaterialCount = approvedRequisitions.reduce(
    (sum, req) => sum + req.lines.filter((l) => l.remainingQty > 0).length,
    0,
  );
  const approvedPrCodes = approvedRequisitions.map((r) => r.prCode).join(", ");

  // Màu sắc huy hiệu Fulfillment Status
  const getFulfillmentBadgeClass = (status: string) => {
    const upper = String(status || "").toUpperCase();
    if (upper.includes("CANCEL") || upper.includes("ĐÃ HỦY")) {
      return "bg-rose-50 text-rose-700 border-rose-200";
    }
    if (upper.includes("IN TRANSIT") || upper.includes("ISSUED") || upper.includes("ĐÃ PHÁT HÀNH")) {
      return "bg-amber-50 text-amber-800 border-amber-200";
    }
    if (upper.includes("CONFIRMED") || upper.includes("MỘT PHẦN")) {
      return "bg-blue-50 text-blue-700 border-blue-200";
    }
    if (
      upper.includes("RECEIVED") ||
      upper.includes("COMMITTED") ||
      upper.includes("FULFILLED") ||
      upper.includes("ĐÃ NHẬN ĐỦ")
    ) {
      return "bg-emerald-50 text-emerald-700 border-emerald-200";
    }
    return "bg-slate-100 text-slate-700 border-slate-200";
  };

  const getFulfillmentDotClass = (status: string) => {
    const upper = String(status || "").toUpperCase();
    if (upper.includes("CANCEL") || upper.includes("ĐÃ HỦY")) {
      return "bg-rose-500";
    }
    if (upper.includes("IN TRANSIT") || upper.includes("ISSUED") || upper.includes("ĐÃ PHÁT HÀNH")) {
      return "bg-amber-500";
    }
    if (upper.includes("CONFIRMED") || upper.includes("MỘT PHẦN")) {
      return "bg-blue-600";
    }
    if (
      upper.includes("RECEIVED") ||
      upper.includes("COMMITTED") ||
      upper.includes("FULFILLED") ||
      upper.includes("ĐÃ NHẬN ĐỦ")
    ) {
      return "bg-emerald-600";
    }
    return "bg-slate-500";
  };

  /** Hiển thị ngày kèm đếm ngược (Còn X ngày xanh dương / Trễ X ngày đỏ / Đã nhận đen đậm). */
  const renderDateWithCountdown = (dateStr: string, isCompleted: boolean) => {
    if (!dateStr) return <span className="text-slate-400">—</span>;
    if (isCompleted) {
      return <span className="font-bold text-slate-900">{dateStr}</span>;
    }

    const todayStr = new Date().toISOString().slice(0, 10);
    const targetMs = new Date(`${dateStr.slice(0, 10)}T00:00:00Z`).getTime();
    const todayMs = new Date(`${todayStr}T00:00:00Z`).getTime();

    if (Number.isNaN(targetMs)) {
      return <span className="font-bold text-slate-900">{dateStr}</span>;
    }

    const diffDays = Math.round((targetMs - todayMs) / 86400000);
    if (diffDays < 0) {
      return (
        <span className="font-bold text-rose-600">
          {dateStr} (Trễ {Math.abs(diffDays)} ngày)
        </span>
      );
    }
    if (diffDays === 0) {
      return <span className="font-bold text-blue-600">{dateStr} (Giao hôm nay)</span>;
    }
    return (
      <span className="font-bold text-blue-600">
        {dateStr} (Còn {diffDays} ngày)
      </span>
    );
  };

  return (
    <>
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
              Gom các yêu cầu mua đã duyệt theo nhà cung cấp, phát hành đơn đặt hàng chính thức và ghi nhận ngày giao hàng mà nhà cung cấp cam kết.
            </p>
          </div>

          {canManagePo && (
            <button
              type="button"
              onClick={() => setShowGeneratePoModal(true)}
              className="px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition self-start sm:self-auto inline-flex items-center gap-2 shrink-0"
            >
              <svg className="w-4 h-4 stroke-[2.5]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <line x1="12" y1="5" x2="12" y2="19" />
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
              <span>Lập đơn mua nguyên liệu</span>
            </button>
          )}
        </div>

        {/* Thông báo động khi có yêu cầu mua hàng mới được Chủ xưởng phê duyệt */}
        {approvedRequisitions.length > 0 && (
          <div className="bg-emerald-50/70 border border-emerald-200 rounded-xl px-5 py-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="text-sm text-slate-800">
              <strong className="font-bold text-slate-900">
                Phê duyệt mới từ Chủ xưởng:
              </strong>{" "}
              Có <strong>{approvedRequisitions.length}</strong> yêu cầu mua hàng đã được phê duyệt (
              <span className="font-mono font-semibold">{approvedPrCodes}</span>
              {pendingMaterialCount > 0
                ? ` — gồm ${pendingMaterialCount} loại nguyên vật liệu`
                : ""}
              ) đang chờ lập đơn mua.
              {canManagePo && (
                <>
                  {" "}Hãy bấm{" "}
                  <strong className="text-blue-700">+ Lập đơn mua nguyên liệu</strong> ở góc trên để phân phối nhà cung cấp và gửi đơn.
                </>
              )}
            </div>
          </div>
        )}

        {/* Khối Active Supplier Purchase Orders */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <h2 className="text-base font-bold text-slate-900">
              Đơn đặt hàng nhà cung cấp đang hoạt động
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
                placeholder="Tìm mã đơn mua, vật tư..."
                className="w-full pl-9 pr-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-2xs"
              />
            </div>
          </div>

          {/* Active Supplier Purchase Orders Table */}
          <div className="overflow-auto max-h-[65vh]">
            <table className="w-full min-w-[960px] text-left border-collapse">
              <thead className="sticky top-0 z-10 bg-[#F8FAFC] shadow-2xs">
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-4 whitespace-nowrap">Mã đơn mua</th>
                  <th className="py-3.5 px-3 whitespace-nowrap">Nhóm vật tư</th>
                  <th className="py-3.5 px-3 min-w-[200px]">Tóm tắt hàng đặt</th>
                  <th className="py-3.5 px-3 whitespace-nowrap">Ngày phát hành</th>
                  <th className="py-3.5 px-3 min-w-[170px]">Ngày giao dự kiến</th>
                  <th className="py-3.5 px-3 whitespace-nowrap">Hạn chót</th>
                  <th className="py-3.5 px-3 whitespace-nowrap">Trạng thái thực hiện</th>
                  <th className="py-3.5 px-4 text-right whitespace-nowrap">Thao tác</th>
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
                    const upperStatus = po.fulfillmentStatus.toUpperCase();
                    const isCancelled =
                      upperStatus.includes("CANCEL") ||
                      upperStatus.includes("ĐÃ HỦY");
                    const isPartial =
                      upperStatus.includes("PARTIAL") ||
                      upperStatus.includes("MỘT PHẦN");
                    const isCompleted =
                      !isPartial &&
                      (upperStatus.includes("RECEIVED") ||
                        upperStatus.includes("COMMITTED") ||
                        upperStatus.includes("FULFILLED") ||
                        upperStatus.includes("ĐÃ NHẬN ĐỦ"));
                    const isConfirmed = upperStatus.includes("CONFIRMED");
                    const canCancel = isOwner && !isCompleted && !isCancelled && !isPartial;

                    return (
                      <tr
                        key={po.poId}
                        className="hover:bg-slate-50/80 transition-colors"
                      >
                        <td className="py-3.5 px-4 font-mono text-xs md:text-sm font-bold text-slate-900 whitespace-nowrap">
                          {po.poCode}
                        </td>
                        <td className="py-3.5 px-3 text-slate-600 whitespace-nowrap">
                          {po.materialCategory}
                        </td>
                        <td className="py-3.5 px-3 text-slate-800 min-w-[210px] max-w-[260px] leading-snug">
                          {po.orderItemsSummary}
                        </td>
                        <td className="py-3.5 px-3 text-slate-600 whitespace-nowrap">{po.issueDate}</td>
                        {/* Cột Ngày giao dự kiến (theo từng nguyên vật liệu) */}
                        <td className="py-3.5 px-3 min-w-[200px] max-w-[280px] leading-snug">
                          {isCancelled ? (
                            <span className="text-slate-400">—</span>
                          ) : po.materialSchedules && po.materialSchedules.length > 0 ? (
                            po.materialSchedules.length === 1 ? (
                              renderDateWithCountdown(
                                po.materialSchedules[0].expectedDeliveryDate,
                                isCompleted,
                              )
                            ) : (
                              <ul className="list-disc list-inside space-y-1 text-xs text-slate-700">
                                {po.materialSchedules.map((ms) => (
                                  <li key={ms.materialId}>
                                    <span>{ms.materialName}: </span>
                                    {renderDateWithCountdown(
                                      ms.expectedDeliveryDate,
                                      isCompleted,
                                    )}
                                  </li>
                                ))}
                              </ul>
                            )
                          ) : (
                            renderDateWithCountdown(po.promisedEtaDate, isCompleted)
                          )}
                        </td>

                        {/* Cột Hạn chót */}
                        <td className="py-3.5 px-3 whitespace-nowrap">
                          {isCancelled ? (
                            <span className="text-slate-400">—</span>
                          ) : po.etaDaysLabel && !isCompleted ? (
                            <span
                              className={`font-bold ${
                                po.etaDaysLabel.toLowerCase().includes("trễ")
                                  ? "text-rose-600"
                                  : "text-blue-600"
                              }`}
                            >
                              {po.promisedEtaDate} ({po.etaDaysLabel})
                            </span>
                          ) : (
                            <span className="font-bold text-slate-900">
                              {po.promisedEtaDate}
                            </span>
                          )}
                        </td>
                        <td className="py-3.5 px-3 whitespace-nowrap">
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
                        <td className="py-3.5 px-4 text-right whitespace-nowrap">
                          <div className="inline-flex items-center justify-end gap-2">
                            {isCancelled ? (
                              <span className="inline-block px-3.5 py-1.5 bg-rose-50 text-rose-500 border border-rose-200 text-xs font-semibold rounded-lg select-none">
                                Đã hủy
                              </span>
                            ) : isCompleted ? (
                              <span className="inline-block px-3.5 py-1.5 bg-slate-50 text-slate-400 border border-slate-200 text-xs font-semibold rounded-lg select-none">
                                Hoàn tất
                              </span>
                            ) : (
                              <>
                                {canCancel && (
                                  <button
                                    type="button"
                                    onClick={() => {
                                      setSelectedPoForCancel(po);
                                      setCancelReason("");
                                      setCancelError("");
                                    }}
                                    className="px-3 py-1.5 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 text-xs font-semibold rounded-lg shadow-2xs transition"
                                  >
                                    Hủy đơn
                                  </button>
                                )}
                                {canManagePo && (
                                  <button
                                    type="button"
                                    onClick={() => setSelectedPoForEta(po)}
                                    className="px-3.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg shadow-2xs transition"
                                  >
                                    {isConfirmed ? "Cập nhật ngày giao" : "Theo dõi hàng về →"}
                                  </button>
                                )}
                                {!canCancel && !canManagePo && (
                                  <span className="inline-block px-3 py-1.5 bg-slate-50 text-slate-400 border border-slate-200 text-xs font-semibold rounded-lg select-none">
                                    Chỉ xem
                                  </span>
                                )}
                              </>
                            )}
                          </div>
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

      {/* Modal Xác nhận hủy đơn đặt hàng trước khi giao */}
      {selectedPoForCancel && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
          <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-md overflow-hidden">
            <div className="px-6 py-4 border-b border-slate-200 bg-rose-50/70 flex items-center justify-between">
              <h2 className="text-base font-bold text-rose-900">
                Xác nhận hủy đơn đặt hàng
              </h2>
              <button
                type="button"
                onClick={() => setSelectedPoForCancel(null)}
                className="text-slate-400 hover:text-slate-600 text-lg font-bold leading-none px-2 py-1 rounded-lg hover:bg-rose-100/60 transition"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleConfirmCancelPo} className="p-6 space-y-4">
              {cancelError && (
                <div className="p-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-700 text-sm font-medium">
                  {cancelError}
                </div>
              )}

              <div className="p-3.5 bg-slate-50 rounded-lg border border-slate-200 text-sm space-y-1">
                <div>
                  <span className="text-slate-500">Mã đơn mua:</span>{" "}
                  <strong className="font-mono text-slate-900">
                    {selectedPoForCancel.poCode}
                  </strong>
                </div>
                <div>
                  <span className="text-slate-500">Hàng đặt:</span>{" "}
                  <span className="text-slate-800">
                    {selectedPoForCancel.orderItemsSummary}
                  </span>
                </div>
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                  Lý do hủy đơn đặt hàng
                </label>
                <textarea
                  rows={3}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder="Ví dụ: Đổi nhà cung cấp khác hoặc thay đổi kế hoạch vật tư..."
                  className="w-full px-3.5 py-2.5 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500"
                />
              </div>

              <div className="pt-2 flex items-center justify-end gap-2.5">
                <button
                  type="button"
                  onClick={() => setSelectedPoForCancel(null)}
                  className="px-4 py-2 text-sm font-semibold text-slate-700 bg-white hover:bg-slate-50 border border-slate-300 rounded-lg transition"
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  disabled={cancelPoMutation.isPending}
                  className="px-4 py-2 text-sm font-bold text-white bg-rose-600 hover:bg-rose-700 rounded-lg shadow-xs transition disabled:opacity-60"
                >
                  {cancelPoMutation.isPending ? "Đang hủy..." : "Xác nhận hủy đơn"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
