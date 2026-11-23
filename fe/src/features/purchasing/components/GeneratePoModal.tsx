import { useEffect, useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  purchasingApi,
  type ActiveSupplierOptionDto,
  type ApprovedRequisitionViewDto,
} from "../api/purchasingApi";

interface GeneratePoModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

const formatMaterialGroupLabel = (group?: string | null): string => {
  switch (String(group || "").toUpperCase()) {
    case "LUMBER":
      return "Gỗ nguyên khối";
    case "HARDWARE":
      return "Phụ kiện kim khí";
    case "FINISH_PAINT":
      return "Sơn & Hóa chất hoàn thiện";
    case "PACKAGING":
      return "Bao bì đóng gói xuất khẩu";
    default:
      return group || "Vật tư sản xuất";
  }
};

const defaultUnitPriceByGroup = (group?: string | null): number => {
  switch (String(group || "").toUpperCase()) {
    case "LUMBER":
      return 12500000;
    case "FINISH_PAINT":
      return 185000;
    case "PACKAGING":
      return 45000;
    default:
      return 15000;
  }
};

const getDefaultFutureDate = (daysAhead = 5): string => {
  const d = new Date();
  d.setDate(d.getDate() + daysAhead);
  return d.toISOString().slice(0, 10);
};

export default function GeneratePoModal({
  isOpen,
  onClose,
  onSuccess,
}: GeneratePoModalProps) {
  const queryClient = useQueryClient();

  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [requisitions, setRequisitions] = useState<ApprovedRequisitionViewDto[]>([]);
  const [suppliers, setSuppliers] = useState<ActiveSupplierOptionDto[]>([]);
  // Không tự động chọn sẵn nhà cung cấp khi mở hộp thoại
  const [selectedSuppliersByItem, setSelectedSuppliersByItem] = useState<
    Record<number, number>
  >({});
  const [etaByItem, setEtaByItem] = useState<Record<number, string>>({});
  const [deadlineEtaDate, setDeadlineEtaDate] = useState(getDefaultFutureDate(7));
  const [hasSubmitted, setHasSubmitted] = useState(false);
  const [errorMsg, setErrorMsg] = useState("");

  useEffect(() => {
    if (!isOpen) return;

    let cancelled = false;
    setIsLoading(true);
    setErrorMsg("");
    setHasSubmitted(false);
    setSelectedSuppliersByItem({});
    setEtaByItem({});
    setDeadlineEtaDate(getDefaultFutureDate(7));

    Promise.all([
      purchasingApi.getApprovedRequisitions(),
      purchasingApi.getActiveSuppliers(),
    ])
      .then(([reqs, activeSups]) => {
        if (cancelled) return;
        setRequisitions(reqs);
        setSuppliers(activeSups);
        setSelectedSuppliersByItem({});
      })
      .catch(() => {
        if (!cancelled) {
          setErrorMsg("Không thể tải danh sách nguyên vật liệu cần mua.");
        }
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [isOpen]);

  const materialItems = useMemo(() => {
    const list: Array<{
      prId: number;
      prCode: string;
      prItemId: number;
      materialId: number;
      materialCode: string;
      materialName: string;
      materialGroup: string;
      unitOfMeasure: string;
      remainingQty: number;
      unitPrice: number;
    }> = [];

    for (const req of requisitions) {
      for (const line of req.lines) {
        if (line.remainingQty > 0) {
          list.push({
            prId: req.prId,
            prCode: req.prCode,
            prItemId: line.prItemId,
            materialId: line.materialId,
            materialCode: line.materialCode,
            materialName: line.materialName,
            materialGroup: line.materialGroup,
            unitOfMeasure: line.unitOfMeasure,
            remainingQty: line.remainingQty,
            unitPrice: defaultUnitPriceByGroup(line.materialGroup),
          });
        }
      }
    }

    return list;
  }, [requisitions]);

  if (!isOpen) return null;

  const handleSelectSupplierForItem = (prItemId: number, supplierId: number) => {
    setSelectedSuppliersByItem((prev) => ({
      ...prev,
      [prItemId]: supplierId,
    }));
    setErrorMsg("");
  };

  const handleEtaChangeForItem = (prItemId: number, etaDate: string) => {
    setEtaByItem((prev) => ({
      ...prev,
      [prItemId]: etaDate,
    }));
    setErrorMsg("");
  };

  const defaultEta = getDefaultFutureDate(5);
  const todayStr = new Date().toISOString().slice(0, 10);

  const unselectedCount = materialItems.filter(
    (it) => (selectedSuppliersByItem[it.prItemId] || 0) <= 0,
  ).length;

  // Gom nhóm các nguyên liệu đã chọn theo từng nhà cung cấp để hiển thị kết quả phân phối
  const distributedGroups = (() => {
    const map = new Map<
      number,
      {
        supplier: ActiveSupplierOptionDto;
        items: typeof materialItems;
      }
    >();

    for (const item of materialItems) {
      const supId = selectedSuppliersByItem[item.prItemId] || 0;
      if (supId <= 0) continue;
      const sup = suppliers.find((s) => s.supplierId === supId);
      if (!sup) continue;

      const existing = map.get(supId);
      if (existing) {
        existing.items.push(item);
      } else {
        map.set(supId, { supplier: sup, items: [item] });
      }
    }

    return Array.from(map.values());
  })();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setHasSubmitted(true);
    setErrorMsg("");

    if (unselectedCount > 0) {
      return;
    }

    if (!deadlineEtaDate || deadlineEtaDate < todayStr) {
      setErrorMsg("Hạn chót giao hàng chung không được bỏ trống hoặc nằm trong quá khứ.");
      return;
    }

    const invalidEtaItem = materialItems.find((it) => {
      const d = etaByItem[it.prItemId] || defaultEta;
      return !d || d < todayStr;
    });
    if (invalidEtaItem) {
      setErrorMsg(
        `Ngày giao hàng của vật liệu "${invalidEtaItem.materialName}" không hợp lệ hoặc nằm trong quá khứ.`,
      );
      return;
    }

    const lateEtaItem = materialItems.find((it) => {
      const d = etaByItem[it.prItemId] || defaultEta;
      return d >= deadlineEtaDate;
    });
    if (lateEtaItem) {
      setErrorMsg(
        `Ngày sớm nhất nhà phân phối giao được của vật liệu "${lateEtaItem.materialName}" phải sớm hơn hạn chót giao hàng chung (${deadlineEtaDate}).`,
      );
      return;
    }

    setIsSubmitting(true);
    try {
      const createdOrders = await purchasingApi.distributeMaterialsToSuppliers({
        promisedEtaDate: deadlineEtaDate,
        lines: materialItems.map((it) => ({
          prId: it.prId,
          prItemId: it.prItemId,
          supplierId: selectedSuppliersByItem[it.prItemId],
          orderedQty: it.remainingQty,
          unitPrice: it.unitPrice,
          expectedArrivalDate: etaByItem[it.prItemId] || defaultEta,
          materialName: it.materialName,
          materialCode: it.materialCode,
          materialGroup: formatMaterialGroupLabel(it.materialGroup),
          unitOfMeasure: it.unitOfMeasure,
        })),
      });

      await queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });

      onSuccess(
        `Đã phát hành và tự động gửi email thành công ${createdOrders.length || distributedGroups.length} đơn đặt hàng tới các nhà cung cấp tương ứng!`,
      );
      onClose();
    } catch (err: unknown) {
      const apiMsg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ||
        "Có lỗi xảy ra khi phát hành đơn đặt hàng từ danh sách nguyên vật liệu.";
      setErrorMsg(apiMsg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px] overflow-y-auto">
      <div className="bg-white rounded-2xl shadow-2xl border border-slate-200 w-full max-w-4xl overflow-hidden my-6">
        <div className="px-6 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
          <div>
            <h2 className="text-base md:text-lg font-extrabold text-slate-900">
              Lập đơn mua nguyên liệu & phân phối nhà cung cấp
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Vui lòng chọn nhà cung cấp tương ứng ở thanh thả xuống bên cạnh từng loại nguyên vật liệu để lập đơn và gửi email.
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

        <form
          onSubmit={handleSubmit}
          className="p-6 space-y-5 max-h-[82vh] overflow-y-auto"
        >
          {errorMsg && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-sm font-semibold">
              {errorMsg}
            </div>
          )}

          {/* Thông tin tổng số nguyên vật liệu cần mua */}
          <div className="bg-blue-50/70 border border-blue-200 rounded-xl px-4 py-3 text-xs md:text-sm text-slate-700">
            Danh sách gồm <strong>{materialItems.length}</strong> loại nguyên vật liệu cần mua từ yêu cầu mua hàng đã duyệt.
          </div>

          {/* Danh sách từng loại nguyên liệu kèm thanh dropdown chọn nhà cung cấp bên cạnh */}
          <div className="space-y-3">
            {isLoading ? (
              <div className="py-10 text-center text-slate-400 text-sm">
                Đang tải danh sách nguyên vật liệu cần mua...
              </div>
            ) : materialItems.length === 0 ? (
              <div className="py-10 px-6 text-center bg-amber-50/70 border border-amber-200 rounded-xl space-y-2">
                <div className="text-sm font-bold text-amber-900">
                  Hiện chưa có yêu cầu mua hàng nào đã được Chủ xưởng duyệt đang chờ lập đơn mua.
                </div>
                <p className="text-xs text-amber-800">
                  Vui lòng nhờ Chủ xưởng (tài khoản <strong>owner</strong>) vào mục{" "}
                  <strong>Yêu cầu mua hàng</strong> để duyệt phiếu đang chờ (ví dụ{" "}
                  <strong>PR-20261104-001</strong>), sau đó quay lại đây để chọn nhà cung cấp và phát hành đơn đặt hàng.
                </p>
              </div>
            ) : (
              materialItems.map((item, idx) => {
                const chosenSupplierId =
                  selectedSuppliersByItem[item.prItemId] || 0;
                const isUnselected = chosenSupplierId <= 0;
                const showUnselectedError = hasSubmitted && isUnselected;
                const chosenSupplier = suppliers.find(
                  (s) => s.supplierId === chosenSupplierId,
                );
                const matchingSuppliers = suppliers.filter(
                  (s) =>
                    String(s.supplyCategory || "").toUpperCase() ===
                    String(item.materialGroup || "").toUpperCase(),
                );
                const dropdownSuppliers =
                  matchingSuppliers.length > 0 ? matchingSuppliers : suppliers;

                return (
                  <div
                    key={item.prItemId}
                    className="p-4 rounded-xl border border-slate-200 bg-slate-50/70 flex flex-col md:flex-row md:items-start justify-between gap-4"
                  >
                    <div className="space-y-1 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-bold text-sm text-slate-900">
                          {idx + 1}. {item.materialName}
                        </span>
                        <span className="font-mono text-xs font-semibold text-slate-500">
                          ({item.materialCode})
                        </span>
                        <span className="px-2 py-0.5 text-[11px] font-bold rounded-full bg-slate-200/80 text-slate-700">
                          {formatMaterialGroupLabel(item.materialGroup)}
                        </span>
                      </div>
                      <div className="text-xs text-slate-600 flex flex-wrap items-center gap-3">
                        <span>
                          Số lượng cần mua:{" "}
                          <strong className="text-slate-900">
                            {item.remainingQty} {item.unitOfMeasure}
                          </strong>
                        </span>
                        <span>•</span>
                        <span>
                          Phiếu nguồn: <strong>{item.prCode}</strong>
                        </span>
                      </div>
                    </div>

                    {/* Thanh dropdown chọn nhà cung cấp ở bên cạnh */}
                    <div className="w-full md:w-80 shrink-0">
                      <select
                        value={chosenSupplierId}
                        onChange={(e) =>
                          handleSelectSupplierForItem(
                            item.prItemId,
                            Number(e.target.value),
                          )
                        }
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
                            {formatMaterialGroupLabel(s.supplyCategory)}
                          </option>
                        ))}
                      </select>

                      {showUnselectedError && (
                        <p className="mt-1 text-xs font-bold text-rose-600">
                          Chưa chọn nhà cung cấp
                        </p>
                      )}
                      {!isUnselected && chosenSupplier && (
                        <p className="mt-1 text-xs text-slate-500">
                          Phân phối cho:{" "}
                          <strong className="text-slate-700">
                            {chosenSupplier.supplierName}
                          </strong>
                        </p>
                      )}

                      {(() => {
                        const itemEta = etaByItem[item.prItemId] || defaultEta;
                        const isLaterThanDeadline = Boolean(
                          deadlineEtaDate && itemEta >= deadlineEtaDate,
                        );
                        return (
                          <div className="mt-2.5 space-y-1">
                            <div className="flex items-center justify-between gap-2">
                              <label className="text-xs font-semibold text-slate-600 whitespace-nowrap">
                                Ngày sớm nhất NCC giao được:
                              </label>
                              <input
                                type="date"
                                min={todayStr}
                                value={itemEta}
                                onChange={(e) =>
                                  handleEtaChangeForItem(
                                    item.prItemId,
                                    e.target.value,
                                  )
                                }
                                className={`px-2.5 py-1.5 text-xs rounded-lg focus:outline-none text-slate-900 ${
                                  isLaterThanDeadline
                                    ? "border-2 border-rose-500 bg-rose-50/40 focus:ring-2 focus:ring-rose-500"
                                    : "border border-slate-300 bg-white focus:ring-2 focus:ring-blue-500"
                                }`}
                              />
                            </div>
                            {isLaterThanDeadline && (
                              <p className="text-[11px] font-bold text-rose-600 text-right">
                                Phải sớm hơn hạn chót giao hàng chung ({deadlineEtaDate})
                              </p>
                            )}
                          </div>
                        );
                      })()}
                    </div>
                  </div>
                );
              })
            )}
          </div>

          {/* Xem trước kết quả gom đơn đặt hàng theo từng nhà cung cấp */}
          {distributedGroups.length > 0 && (
            <div className="bg-emerald-50/60 border border-emerald-200 rounded-xl p-4 space-y-2.5">
              <div className="text-xs font-bold uppercase tracking-wider text-emerald-800">
                Kết quả phân phối ({distributedGroups.length} đơn đặt hàng sẽ được tạo riêng &amp; tự động gửi email tới nhà cung cấp)
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {distributedGroups.map((grp) => (
                  <div
                    key={grp.supplier.supplierId}
                    className="bg-white p-3 rounded-lg border border-emerald-200 text-xs space-y-1"
                  >
                    <div className="font-bold text-slate-900">
                      {grp.supplier.supplierName}
                    </div>
                    <div className="text-[11px] text-blue-700 font-medium">
                      Email tự động nhận đơn: {grp.supplier.contactEmail || "Chưa có email"}
                    </div>
                    <ul className="text-slate-600 list-disc list-inside space-y-0.5">
                      {grp.items.map((it) => (
                        <li key={it.prItemId}>
                          {it.materialName}:{" "}
                          <strong>
                            {it.remainingQty} {it.unitOfMeasure}
                          </strong>{" "}
                          <span className="text-slate-500">
                            (Giao: {etaByItem[it.prItemId] || defaultEta})
                          </span>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Hạn chót giao hàng chung */}
          <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div>
              <label className="block text-sm font-bold text-slate-800">
                Hạn chót giao hàng chung
              </label>
              <p className="text-xs text-slate-500 mt-0.5">
                Ngày sớm nhất từng nhà phân phối giao được phải luôn sớm hơn mốc hạn chót này.
              </p>
            </div>
            <input
              type="date"
              min={todayStr}
              value={deadlineEtaDate}
              onChange={(e) => {
                setDeadlineEtaDate(e.target.value);
                setErrorMsg("");
              }}
              className="w-full sm:w-52 px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 font-semibold text-slate-900 shrink-0"
            />
          </div>

          {errorMsg && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-sm font-semibold">
              {errorMsg}
            </div>
          )}

          <div className="pt-3 border-t border-slate-200 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-semibold text-slate-600 hover:text-slate-800 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={isSubmitting || isLoading || materialItems.length === 0}
              className="px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-xs transition disabled:opacity-60"
            >
              {isSubmitting ? "Đang phát hành..." : "Phát hành đơn mua hàng"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
