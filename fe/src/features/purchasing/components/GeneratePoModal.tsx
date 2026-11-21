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
  const [sortByGroup, setSortByGroup] = useState(false);
  const [hasSubmitted, setHasSubmitted] = useState(false);
  const [promisedEtaDate, setPromisedEtaDate] = useState(getDefaultFutureDate(5));
  const [errorMsg, setErrorMsg] = useState("");

  useEffect(() => {
    if (!isOpen) return;

    let cancelled = false;
    setIsLoading(true);
    setErrorMsg("");
    setHasSubmitted(false);
    setSortByGroup(false);
    setSelectedSuppliersByItem({});
    setPromisedEtaDate(getDefaultFutureDate(5));

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

    if (sortByGroup) {
      return [...list].sort((a, b) =>
        a.materialGroup.localeCompare(b.materialGroup),
      );
    }

    return list;
  }, [requisitions, sortByGroup]);

  if (!isOpen) return null;

  const handleSelectSupplierForItem = (prItemId: number, supplierId: number) => {
    setSelectedSuppliersByItem((prev) => ({
      ...prev,
      [prItemId]: supplierId,
    }));
    setErrorMsg("");
  };

  /** Nút sắp xếp & phân phối nguyên liệu cần mua cho nhà cung cấp tương ứng theo nhóm vật tư */
  const handleSortAndDistributeByGroup = () => {
    setSortByGroup(true);
    const nextMap: Record<number, number> = { ...selectedSuppliersByItem };

    for (const item of materialItems) {
      const matchedSupplier =
        suppliers.find(
          (s) =>
            String(s.supplyCategory || "").toUpperCase() ===
            String(item.materialGroup || "").toUpperCase(),
        ) || suppliers[0];

      if (matchedSupplier) {
        nextMap[item.prItemId] = matchedSupplier.supplierId;
      }
    }

    setSelectedSuppliersByItem(nextMap);
    setErrorMsg("");
  };

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

    if (!promisedEtaDate) {
      setErrorMsg("Vui lòng chọn ngày nhà cung cấp cam kết giao hàng.");
      return;
    }

    setIsSubmitting(true);
    try {
      const createdOrders = await purchasingApi.distributeMaterialsToSuppliers({
        promisedEtaDate,
        lines: materialItems.map((it) => ({
          prId: it.prId,
          prItemId: it.prItemId,
          supplierId: selectedSuppliersByItem[it.prItemId],
          orderedQty: it.remainingQty,
          unitPrice: it.unitPrice,
          materialName: it.materialName,
          materialCode: it.materialCode,
          materialGroup: formatMaterialGroupLabel(it.materialGroup),
          unitOfMeasure: it.unitOfMeasure,
        })),
      });

      await queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });

      onSuccess(
        `Đã phân phối nguyên liệu và phát hành thành công ${createdOrders.length || distributedGroups.length} đơn đặt hàng cho các nhà cung cấp tương ứng!`,
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
              Chọn nhà cung cấp ở thanh thả xuống bên cạnh từng loại vật liệu hoặc bấm nút sắp xếp & phân phối theo nhóm vật tư.
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

          {/* Thanh công cụ Sắp xếp & phân phối nguyên liệu */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-blue-50/70 border border-blue-200 rounded-xl px-4 py-3">
            <div className="text-xs md:text-sm text-slate-700">
              Danh sách gồm <strong>{materialItems.length}</strong> loại nguyên vật liệu cần mua từ yêu cầu mua hàng đã duyệt.
            </div>
            <button
              type="button"
              onClick={handleSortAndDistributeByGroup}
              disabled={isLoading || materialItems.length === 0}
              className="px-3.5 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-lg shadow-2xs transition shrink-0 inline-flex items-center gap-1.5 disabled:opacity-60"
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
                  d="M3 4h13M3 8h9m-9 4h6m4 0l4-4m0 0l4 4m-4-4v12"
                />
              </svg>
              <span>Sắp xếp & phân phối theo nhóm vật tư</span>
            </button>
          </div>

          {/* Danh sách từng loại nguyên liệu kèm thanh dropdown chọn nhà cung cấp bên cạnh */}
          <div className="space-y-3">
            {isLoading ? (
              <div className="py-10 text-center text-slate-400 text-sm">
                Đang tải danh sách nguyên vật liệu cần mua...
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
                        {suppliers.map((s) => (
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
                Kết quả phân phối ({distributedGroups.length} đơn đặt hàng sẽ được tạo riêng theo nhà cung cấp)
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
                    <ul className="text-slate-600 list-disc list-inside space-y-0.5">
                      {grp.items.map((it) => (
                        <li key={it.prItemId}>
                          {it.materialName}:{" "}
                          <strong>
                            {it.remainingQty} {it.unitOfMeasure}
                          </strong>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Ngày giao cam kết */}
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1.5">
              Ngày nhà cung cấp cam kết giao hàng
            </label>
            <input
              type="date"
              value={promisedEtaDate}
              onChange={(e) => setPromisedEtaDate(e.target.value)}
              className="w-full sm:w-64 px-3.5 py-2.5 text-sm border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

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
              disabled={isSubmitting || isLoading}
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
