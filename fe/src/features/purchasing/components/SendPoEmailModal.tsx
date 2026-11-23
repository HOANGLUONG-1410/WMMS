import { useEffect, useMemo, useState } from "react";
import type { SupplierPurchaseOrderDto } from "../types";
import {
  purchasingApi,
  type ActiveSupplierOptionDto,
  type PoLineItemDto,
  type PurchaseOrderDetailViewDto,
} from "../api/purchasingApi";

interface SendPoEmailModalProps {
  purchaseOrder: SupplierPurchaseOrderDto | null;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

const DEFAULT_DELIVERY_ADDRESS =
  "Kho nguyên vật liệu — Công ty TNHH Nguyên Khoa Hà Nam, KCN Châu Sơn, TP. Phủ Lý, Tỉnh Hà Nam";
const DEFAULT_PAYMENT_NOTE =
  "Thanh toán trước 50% giá trị hợp đồng, 50% còn lại thanh toán sau khi giao hàng.";

const formatSupplyCategoryLabel = (category?: string | null): string => {
  switch (String(category || "").toUpperCase()) {
    case "LUMBER":
      return "Gỗ nguyên khối";
    case "HARDWARE":
      return "Phụ kiện kim khí";
    case "FINISH_PAINT":
      return "Sơn & Hóa chất";
    case "PACKAGING":
      return "Bao bì đóng gói";
    default:
      return category || "Vật tư";
  }
};

const formatVnd = (value: number): string =>
  new Intl.NumberFormat("vi-VN").format(Math.round(value));

export default function SendPoEmailModal({
  purchaseOrder,
  onClose,
  onSuccess,
}: SendPoEmailModalProps) {
  const [isLoadingData, setIsLoadingData] = useState(false);
  const [isSending, setIsSending] = useState(false);
  const [poDetail, setPoDetail] = useState<PurchaseOrderDetailViewDto | null>(null);
  const [suppliers, setSuppliers] = useState<ActiveSupplierOptionDto[]>([]);
  // Không tự động chọn nhà cung cấp: người dùng phải chủ động chọn nhà cung cấp cho từng loại vật liệu
  const [selectedSuppliersByLine, setSelectedSuppliersByLine] = useState<
    Record<number, number>
  >({});
  const [hasSubmitted, setHasSubmitted] = useState(false);
  const [deliveryAddress, setDeliveryAddress] = useState(DEFAULT_DELIVERY_ADDRESS);
  const [paymentMethodNote, setPaymentMethodNote] = useState(DEFAULT_PAYMENT_NOTE);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    if (!purchaseOrder) return;

    let cancelled = false;
    setIsLoadingData(true);
    setErrorMsg(null);
    setHasSubmitted(false);
    setSelectedSuppliersByLine({});

    Promise.all([
      purchasingApi.getActiveSuppliers(),
      purchasingApi.getPurchaseOrderById(purchaseOrder.poId, purchaseOrder),
    ])
      .then(([activeSuppliers, detail]) => {
        if (cancelled) return;
        setSuppliers(activeSuppliers);
        setPoDetail(detail);
        // Giữ trạng thái chưa chọn nhà cung cấp cho tất cả các dòng vật liệu
        setSelectedSuppliersByLine({});
        setDeliveryAddress(DEFAULT_DELIVERY_ADDRESS);
        setPaymentMethodNote(detail.terms?.trim() || DEFAULT_PAYMENT_NOTE);
      })
      .catch(() => {
        if (!cancelled) {
          setErrorMsg("Không thể tải chi tiết đơn đặt hàng. Vui lòng thử lại.");
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoadingData(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [purchaseOrder]);

  const lines: PoLineItemDto[] = useMemo(() => {
    if (poDetail?.lines && poDetail.lines.length > 0) {
      return poDetail.lines;
    }
    if (!purchaseOrder) return [];
    return [
      {
        poDetailId: 1,
        materialId: 1,
        materialCode: "NVL-01",
        materialName: purchaseOrder.orderItemsSummary || "Nguyên vật liệu theo đơn",
        unitOfMeasure: "Lô",
        orderedQty: 1,
        receivedQty: 0,
        remainingQty: 1,
        unitPrice: poDetail?.totalAmount || 12500000,
        lineAmount: poDetail?.totalAmount || 12500000,
      },
    ];
  }, [poDetail, purchaseOrder]);

  if (!purchaseOrder) return null;

  const handleLineSupplierChange = (poDetailId: number, supplierId: number) => {
    setSelectedSuppliersByLine((prev) => ({
      ...prev,
      [poDetailId]: supplierId,
    }));
    setErrorMsg(null);
  };

  const computedLines = lines.map((line, idx) => {
    const unitPrice =
      line.unitPrice && line.unitPrice > 0 ? line.unitPrice : 12500000;
    const lineTotal =
      line.lineAmount && line.lineAmount > 0
        ? line.lineAmount
        : Math.round(line.orderedQty * unitPrice);
    const chosenSupplierId = selectedSuppliersByLine[line.poDetailId] || 0;
    const chosenSupplier =
      suppliers.find((s) => s.supplierId === chosenSupplierId) || null;

    return {
      poDetailId: line.poDetailId,
      stt: idx + 1,
      materialCode: line.materialCode,
      materialName: line.materialName,
      materialGroup: line.materialGroup || "",
      fullName: line.materialCode
        ? `${line.materialName} (${line.materialCode})`
        : line.materialName,
      uom: line.unitOfMeasure || "Đơn vị",
      qty: line.orderedQty,
      unitPrice,
      lineTotal,
      chosenSupplierId,
      chosenSupplier,
    };
  });

  // Gom nhóm các vật liệu đã được chọn nhà cung cấp để hiển thị biểu mẫu ĐƠN ĐẶT HÀNG tương ứng
  const groupedBySupplier = (() => {
    const map = new Map<
      number,
      {
        supplier: ActiveSupplierOptionDto;
        rows: typeof computedLines;
      }
    >();

    for (const item of computedLines) {
      if (item.chosenSupplierId > 0 && item.chosenSupplier) {
        const existing = map.get(item.chosenSupplierId);
        if (existing) {
          existing.rows.push(item);
        } else {
          map.set(item.chosenSupplierId, {
            supplier: item.chosenSupplier,
            rows: [item],
          });
        }
      }
    }

    return Array.from(map.values());
  })();

  const unselectedCount = computedLines.filter(
    (item) => item.chosenSupplierId <= 0,
  ).length;

  const etaDisplay = (() => {
    const raw = poDetail?.promisedEtaDate || purchaseOrder.promisedEtaDate || "";
    if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) {
      const [y, m, d] = raw.split("-");
      return `${d}/${m}/${y}`;
    }
    return raw || "Theo thỏa thuận";
  })();

  const now = new Date();
  const dayStr = String(now.getDate()).padStart(2, "0");
  const monthStr = String(now.getMonth() + 1).padStart(2, "0");
  const yearStr = String(now.getFullYear());

  const handleSendEmail = async (e: React.FormEvent) => {
    e.preventDefault();
    setHasSubmitted(true);
    setErrorMsg(null);

    if (unselectedCount > 0) {
      return;
    }

    setIsSending(true);
    try {
      const materialAssignments = computedLines.map((item) => ({
        poDetailId: item.poDetailId,
        supplierId: item.chosenSupplierId,
      }));

      const firstSupplierId = materialAssignments[0]?.supplierId;
      const result = await purchasingApi.sendPurchaseOrderEmail(
        purchaseOrder.poId,
        {
          supplierId: firstSupplierId,
          materialAssignments,
          deliveryAddress: deliveryAddress.trim(),
          paymentMethodNote: paymentMethodNote.trim(),
        },
      );
      onSuccess(result.message);
      onClose();
    } catch (err: unknown) {
      const apiMessage =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ||
        "Có lỗi xảy ra khi gửi email đơn đặt hàng cho nhà cung cấp.";
      setErrorMsg(apiMessage);
    } finally {
      setIsSending(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-xs p-4 overflow-y-auto">
      <div className="bg-white rounded-2xl border border-slate-200 shadow-2xl w-full max-w-4xl overflow-hidden my-6">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between bg-slate-50">
          <div>
            <h3 className="text-lg font-extrabold text-slate-900">
              Gửi email Đơn đặt hàng nguyên vật liệu cho nhà cung cấp
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Vui lòng chọn nhà cung cấp tương ứng ở thanh thả xuống bên cạnh từng loại vật liệu để gửi đơn đặt hàng.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 font-bold text-lg px-2"
          >
            ✕
          </button>
        </div>

        <form
          onSubmit={handleSendEmail}
          className="p-6 space-y-5 max-h-[82vh] overflow-y-auto"
        >
          {errorMsg && (
            <div className="px-4 py-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-sm font-semibold">
              {errorMsg}
            </div>
          )}

          {/* Danh sách từng loại vật liệu kèm thanh dropdown chọn nhà cung cấp bên cạnh */}
          <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 space-y-3">
            <div className="flex items-center justify-between">
              <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700">
                Chọn nhà cung cấp cho từng loại vật liệu trong đơn ({computedLines.length} mặt hàng)
              </h4>
              {unselectedCount === 0 && (
                <span className="text-xs font-bold text-emerald-700">
                  Đã chọn đủ nhà cung cấp cho tất cả vật liệu
                </span>
              )}
            </div>

            <div className="space-y-3">
              {computedLines.map((item) => {
                const isUnselected = item.chosenSupplierId <= 0;
                const showUnselectedError = hasSubmitted && isUnselected;
                const matchingSuppliers = item.materialGroup
                  ? suppliers.filter(
                      (s) =>
                        String(s.supplyCategory || "").toUpperCase() ===
                        String(item.materialGroup || "").toUpperCase(),
                    )
                  : [];
                const dropdownSuppliers =
                  matchingSuppliers.length > 0 ? matchingSuppliers : suppliers;

                return (
                  <div
                    key={item.poDetailId}
                    className="bg-white p-3.5 rounded-xl border border-slate-200 flex flex-col md:flex-row md:items-start justify-between gap-4"
                  >
                    {/* Thông tin vật liệu bên trái */}
                    <div className="space-y-1 flex-1">
                      <div className="font-bold text-sm text-slate-900">
                        {item.stt}. {item.materialName}
                        {item.materialCode && (
                          <span className="ml-2 font-mono text-xs font-semibold text-slate-500">
                            ({item.materialCode})
                          </span>
                        )}
                      </div>
                      <div className="text-xs text-slate-600 flex flex-wrap items-center gap-3">
                        <span>
                          Số lượng: <strong>{item.qty}</strong> {item.uom}
                        </span>
                        <span>•</span>
                        <span>
                          Đơn giá: <strong>{formatVnd(item.unitPrice)} đ</strong>
                        </span>
                        <span>•</span>
                        <span>
                          Thành tiền:{" "}
                          <strong className="text-slate-900">
                            {formatVnd(item.lineTotal)} đ
                          </strong>
                        </span>
                      </div>
                    </div>

                    {/* Thanh dropdown chọn nhà cung cấp ở bên cạnh */}
                    <div className="w-full md:w-80 shrink-0">
                      <select
                        value={item.chosenSupplierId}
                        onChange={(e) =>
                          handleLineSupplierChange(
                            item.poDetailId,
                            Number(e.target.value),
                          )
                        }
                        disabled={isLoadingData}
                        aria-invalid={showUnselectedError}
                        className={`w-full px-3.5 py-2 text-sm rounded-lg focus:outline-none font-medium transition ${
                          showUnselectedError
                            ? "border-2 border-rose-500 bg-rose-50/40 text-rose-900 focus:ring-2 focus:ring-rose-500"
                            : "border border-slate-300 bg-white text-slate-900 focus:ring-2 focus:ring-blue-500"
                        }`}
                      >
                        <option value={0}>-- Chọn nhà cung cấp --</option>
                        {dropdownSuppliers.map((s) => (
                          <option key={s.supplierId} value={s.supplierId}>
                            {s.supplierName} —{" "}
                            {formatSupplyCategoryLabel(s.supplyCategory)}
                          </option>
                        ))}
                      </select>

                      {showUnselectedError && (
                        <p className="mt-1 text-xs font-bold text-rose-600">
                          Chưa chọn nhà cung cấp
                        </p>
                      )}
                      {!isUnselected && (
                        <p className="mt-1 text-xs text-slate-500">
                          Email nhận:{" "}
                          <strong className="text-slate-700">
                            {item.chosenSupplier?.contactEmail ||
                              "Chưa có email"}
                          </strong>
                        </p>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Địa điểm giao hàng & điều khoản thanh toán */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2 border-t border-slate-200">
              <div>
                <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1.5">
                  Địa điểm giao hàng ghi trên đơn
                </label>
                <input
                  type="text"
                  value={deliveryAddress}
                  onChange={(e) => setDeliveryAddress(e.target.value)}
                  className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-slate-800"
                />
              </div>

              <div>
                <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1.5">
                  Điều khoản thanh toán ghi trên đơn
                </label>
                <input
                  type="text"
                  value={paymentMethodNote}
                  onChange={(e) => setPaymentMethodNote(e.target.value)}
                  className="w-full px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-slate-800"
                />
              </div>
            </div>
          </div>

          {/* Bản xem trước biểu mẫu ĐƠN ĐẶT HÀNG theo từng nhà cung cấp được chọn */}
          <div>
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                Xem trước nội dung email & tệp Đơn đặt hàng gửi nhà cung cấp
              </span>
              <span className="text-xs text-emerald-700 font-semibold bg-emerald-50 border border-emerald-200 px-2.5 py-0.5 rounded-full">
                Đính kèm biểu mẫu ĐƠN ĐẶT HÀNG (.doc)
              </span>
            </div>

            {groupedBySupplier.length === 0 ? (
              <div
                className="bg-white border-2 border-slate-300 rounded-xl p-6 md:p-8 shadow-inner text-slate-950"
                style={{ fontFamily: "'Times New Roman', Times, serif" }}
              >
                <div className="text-center mb-5">
                  <div className="font-bold text-base md:text-lg uppercase tracking-wide">
                    CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
                  </div>
                  <div className="font-bold text-sm md:text-base underline underline-offset-4 mt-0.5">
                    Độc lập – Tự do – Hạnh phúc
                  </div>
                </div>

                <div className="text-center my-5">
                  <div className="font-bold text-xl md:text-2xl uppercase">
                    ĐƠN ĐẶT HÀNG
                  </div>
                  <div className="italic text-sm md:text-base mt-1">
                    Số: <strong>{purchaseOrder.poCode}</strong>
                  </div>
                </div>

                <div className="space-y-1.5 text-[15px] leading-relaxed mb-4">
                  <p>
                    <strong>Kính gửi:</strong>{" "}
                    <span className="text-rose-600 font-bold italic">
                      (Chưa chọn nhà cung cấp)
                    </span>
                  </p>
                  <p>
                    <strong>Công ty TNHH Nguyên Khoa Hà Nam</strong> có nhu cầu
                    đặt hàng tại Quý công ty theo mẫu yêu cầu.
                  </p>
                  <p>Nội dung đặt hàng như sau:</p>
                </div>

                <div className="overflow-x-auto my-3">
                  <table className="w-full border-collapse border border-slate-800 text-[14.5px]">
                    <thead>
                      <tr className="bg-slate-100 font-bold text-center">
                        <th className="border border-slate-800 px-2.5 py-2 w-12">
                          STT
                        </th>
                        <th className="border border-slate-800 px-3 py-2 text-left">
                          Tên mặt hàng
                        </th>
                        <th className="border border-slate-800 px-2.5 py-2 w-20">
                          ĐVT
                        </th>
                        <th className="border border-slate-800 px-2.5 py-2 w-24">
                          Số lượng
                        </th>
                        <th className="border border-slate-800 px-3 py-2 w-32">
                          Đơn giá
                        </th>
                        <th className="border border-slate-800 px-3 py-2 w-36">
                          Thành tiền
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {computedLines.map((row, idx) => (
                        <tr key={row.poDetailId}>
                          <td className="border border-slate-800 px-2.5 py-2 text-center">
                            {idx + 1}
                          </td>
                          <td className="border border-slate-800 px-3 py-2">
                            {row.fullName}
                          </td>
                          <td className="border border-slate-800 px-2.5 py-2 text-center">
                            {row.uom}
                          </td>
                          <td className="border border-slate-800 px-2.5 py-2 text-right">
                            {row.qty}
                          </td>
                          <td className="border border-slate-800 px-3 py-2 text-right">
                            {formatVnd(row.unitPrice)} đ
                          </td>
                          <td className="border border-slate-800 px-3 py-2 text-right font-bold">
                            {formatVnd(row.lineTotal)} đ
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : (
              <div className="space-y-6">
                {groupedBySupplier.map((group) => {
                  const groupTotal = group.rows.reduce(
                    (sum, r) => sum + r.lineTotal,
                    0,
                  );
                  return (
                    <div
                      key={group.supplier.supplierId}
                      className="bg-white border-2 border-slate-300 rounded-xl p-6 md:p-8 shadow-inner text-slate-950"
                      style={{ fontFamily: "'Times New Roman', Times, serif" }}
                    >
                      <div className="text-center mb-5">
                        <div className="font-bold text-base md:text-lg uppercase tracking-wide">
                          CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
                        </div>
                        <div className="font-bold text-sm md:text-base underline underline-offset-4 mt-0.5">
                          Độc lập – Tự do – Hạnh phúc
                        </div>
                      </div>

                      <div className="text-center my-5">
                        <div className="font-bold text-xl md:text-2xl uppercase">
                          ĐƠN ĐẶT HÀNG
                        </div>
                        <div className="italic text-sm md:text-base mt-1">
                          Số: <strong>{purchaseOrder.poCode}</strong>
                        </div>
                      </div>

                      <div className="space-y-1.5 text-[15px] leading-relaxed mb-4">
                        <p>
                          <strong>Kính gửi:</strong>{" "}
                          <strong className="text-blue-950">
                            {group.supplier.supplierName}
                          </strong>
                        </p>
                        <p>
                          <strong>Công ty TNHH Nguyên Khoa Hà Nam</strong> có
                          nhu cầu đặt hàng tại Quý công ty theo mẫu yêu cầu.
                        </p>
                        <p>Nội dung đặt hàng như sau:</p>
                      </div>

                      <div className="overflow-x-auto my-3">
                        <table className="w-full border-collapse border border-slate-800 text-[14.5px]">
                          <thead>
                            <tr className="bg-slate-100 font-bold text-center">
                              <th className="border border-slate-800 px-2.5 py-2 w-12">
                                STT
                              </th>
                              <th className="border border-slate-800 px-3 py-2 text-left">
                                Tên mặt hàng
                              </th>
                              <th className="border border-slate-800 px-2.5 py-2 w-20">
                                ĐVT
                              </th>
                              <th className="border border-slate-800 px-2.5 py-2 w-24">
                                Số lượng
                              </th>
                              <th className="border border-slate-800 px-3 py-2 w-32">
                                Đơn giá
                              </th>
                              <th className="border border-slate-800 px-3 py-2 w-36">
                                Thành tiền
                              </th>
                            </tr>
                          </thead>
                          <tbody>
                            {group.rows.map((row, idx) => (
                              <tr key={row.poDetailId}>
                                <td className="border border-slate-800 px-2.5 py-2 text-center">
                                  {idx + 1}
                                </td>
                                <td className="border border-slate-800 px-3 py-2">
                                  {row.fullName}
                                </td>
                                <td className="border border-slate-800 px-2.5 py-2 text-center">
                                  {row.uom}
                                </td>
                                <td className="border border-slate-800 px-2.5 py-2 text-right">
                                  {row.qty}
                                </td>
                                <td className="border border-slate-800 px-3 py-2 text-right">
                                  {formatVnd(row.unitPrice)} đ
                                </td>
                                <td className="border border-slate-800 px-3 py-2 text-right font-bold">
                                  {formatVnd(row.lineTotal)} đ
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>

                      <div className="space-y-1.5 text-[15px] leading-relaxed mt-4">
                        <p className="font-bold text-base">
                          Tổng cộng: {formatVnd(groupTotal)} VNĐ
                        </p>
                        <p>
                          <strong>Thời gian giao hàng:</strong> {etaDisplay}
                        </p>
                        <p>
                          <strong>Địa điểm giao hàng:</strong> {deliveryAddress}
                        </p>
                        <div>
                          <strong>Phương thức thanh toán:</strong>
                          <div className="pl-3 mt-0.5">
                            - Thanh toán bằng tiền mặt hoặc chuyển khoản
                          </div>
                          <div className="pl-3">- {paymentMethodNote}</div>
                        </div>
                      </div>

                      <div className="flex justify-end mt-6">
                        <div className="text-center min-w-[230px]">
                          <div className="italic text-sm">
                            Hà Nam, ngày {dayStr} tháng {monthStr} năm {yearStr}
                          </div>
                          <div className="font-bold uppercase mt-1">
                            GIÁM ĐỐC
                          </div>
                          <div className="font-bold mt-10 text-sm">
                            Công ty TNHH Nguyên Khoa Hà Nam
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* Footer Buttons */}
          <div className="pt-3 border-t border-slate-200 flex flex-col sm:flex-row items-center justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2.5 text-sm font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition w-full sm:w-auto"
            >
              Đóng
            </button>
            <button
              type="submit"
              disabled={isSending || isLoadingData}
              className="px-5 py-2.5 text-sm font-bold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-xs transition disabled:opacity-60 inline-flex items-center justify-center gap-2 w-full sm:w-auto"
            >
              <svg
                className="w-4 h-4"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.2"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M3 8l9 6 9-6M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
                />
              </svg>
              <span>
                {isSending
                  ? "Đang gửi email đơn đặt hàng..."
                  : "Gửi email đơn đặt hàng"}
              </span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
