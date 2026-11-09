import { useEffect, useState } from "react";
import { isAxiosError } from "axios";
import { bomApi } from "../api/bomApi";
import {
  useApproveBom,
  useCalculateDemand,
  useOrderItemBom,
  useSaveBomDraft,
} from "../hooks/useBom";
import type {
  BomDetailDto,
  BomDto,
  OrderSpaceLineItemForBom,
  RoutingStageDto,
} from "../types";
import VisualProductCatalogModal, {
  type VisualModelOption,
} from "./VisualProductCatalogModal";
import MaterialItemModal from "./MaterialItemModal";
import BomStagingModal from "./BomStagingModal";
import { bomStatusLabel } from "../../../lib/statusLabels";

interface ProductBomModalProps {
  isOpen: boolean;
  lineItem: OrderSpaceLineItemForBom | null;
  spaceKey: string;
  onClose: () => void;
  onSaved: () => void;
}

export default function ProductBomModal({
  isOpen,
  lineItem,
  spaceKey,
  onClose,
  onSaved,
}: ProductBomModalProps) {
  const orderItemId = lineItem?.orderItemId || 0;

  const {
    data: serverBom,
    isLoading: isLoadingBom,
    refetch: refetchOrderItemBom,
  } = useOrderItemBom(isOpen ? orderItemId : 0);

  const calculateDemandMutation = useCalculateDemand();
  const saveBomMutation = useSaveBomDraft();
  const approveBomMutation = useApproveBom();

  const [activeBomId, setActiveBomId] = useState<number | null>(null);
  const [bomStatus, setBomStatus] = useState<string>("NOT_CREATED");
  const [lastSaved, setLastSaved] = useState<string>("Chưa tạo trong CSDL");
  const [scrapRate, setScrapRate] = useState<number>(3.5);
  const [bomItems, setBomItems] = useState<BomDetailDto[]>([]);
  const [routingStages, setRoutingStages] = useState<RoutingStageDto[]>([]);

  const [showVisualCatalogModal, setShowVisualCatalogModal] = useState(false);
  const [showMaterialModal, setShowMaterialModal] = useState(false);
  const [showStagingModal, setShowStagingModal] = useState(false);
  const [editingMaterialItem, setEditingMaterialItem] = useState<BomDetailDto | null>(null);

  const [toastBanner, setToastBanner] = useState<{
    type: "success" | "error" | "info";
    text: string;
  } | null>(null);

  const isLockedForProduction =
    bomStatus === "READY_FOR_PRODUCTION" || bomStatus === "LOCKED";

  useEffect(() => {
    if (!isOpen || !lineItem) return;
    setToastBanner(null);
  }, [isOpen, lineItem?.orderItemId]);

  useEffect(() => {
    if (!isOpen || !lineItem) return;

    if (serverBom) {
      setActiveBomId(serverBom.bomId);
      setBomStatus(serverBom.bomStatusText || String(serverBom.bomStatus || "DRAFT"));
      setLastSaved(
        serverBom.updatedAt
          ? new Date(serverBom.updatedAt).toLocaleString("vi-VN")
          : serverBom.createdAt
            ? new Date(serverBom.createdAt).toLocaleString("vi-VN")
            : "Đã đồng bộ từ máy chủ",
      );
      setScrapRate(serverBom.scrapRatePercent || 3.5);
      setBomItems(serverBom.bomDetails || []);
      setRoutingStages(serverBom.routingStages || []);
      return;
    }

    if (!isLoadingBom && lineItem.orderItemId > 0) {
      setActiveBomId(null);
      setBomStatus("NOT_CREATED");
      setLastSaved("Chưa tạo trong CSDL");

      bomApi
        .checkProductBom(lineItem.productId)
        .then(async (res) => {
          const inherited = res?.data;
          if (inherited?.hasExistingBom && inherited.details?.length > 0) {
            setScrapRate(inherited.scrapRatePercent || 3.5);
            setBomItems(inherited.details);
            setRoutingStages(inherited.routingStages || []);
            setToastBanner({
              type: "info",
              text:
                res.message ||
                `Dòng hàng #${lineItem.orderItemId} chưa có BOM riêng. Hệ thống đã gợi ý định mức kế thừa từ BOM #${inherited.sourceBomId} của sản phẩm ${lineItem.productSku}. Nhấn "Lưu BOM nháp" để lưu.`,
            });
          } else {
            setBomItems([]);
            setScrapRate(3.5);
            try {
              const routeRes = await bomApi.getProductRouting(lineItem.productId);
              if (routeRes?.data?.length) {
                setRoutingStages(routeRes.data);
              } else {
                const defRes = await bomApi.getDefaultRouting(lineItem.productId);
                setRoutingStages(defRes?.data || []);
              }
            } catch {
              setRoutingStages([]);
            }
          }
        })
        .catch(() => {
          setBomItems([]);
          setRoutingStages([]);
        });
    }
  }, [isOpen, lineItem, serverBom, isLoadingBom]);

  if (!isOpen || !lineItem) return null;

  const formatUnitLabel = (uom: string, value: number) => {
    if (uom === "Sheet" && value !== 1) return "Sheets";
    if (uom === "Box" && value !== 1) return "Boxes";
    return uom;
  };

  const computeRowMetrics = (item: BomDetailDto, orderQty: number, ratePercent: number) => {
    const uomUpper = (item.unitOfMeasure || "").toUpperCase();
    const isDecimalUnit =
      uomUpper === "M3" || uomUpper === "M³" || uomUpper === "LITER" || uomUpper === "KG";
    const netSubtotal = item.netQtyPerUnit * orderQty;
    const scrapRaw = netSubtotal * (ratePercent / 100);
    const calculatedGross = Number((netSubtotal * (1 + ratePercent / 100)).toFixed(4));
    const grossRaw =
      item.grossTotalDemand && item.grossTotalDemand > 0
        ? item.grossTotalDemand
        : calculatedGross;

    if (isDecimalUnit) {
      const netFormatted = netSubtotal.toFixed(4);
      const scrapFormatted = scrapRaw.toFixed(4);
      const grossFormatted = Number(grossRaw).toFixed(4);
      return {
        netSubtotalText: `${netFormatted} ${item.unitOfMeasure}`,
        scrapAllowanceText: `+${ratePercent}% (${scrapFormatted} ${item.unitOfMeasure})`,
        grossTotalText: `${grossFormatted} ${item.unitOfMeasure}`,
      };
    }

    const netRounded = Math.round(netSubtotal * 100) / 100;
    const scrapRounded = Number(scrapRaw.toFixed(2));
    const grossRounded = Number(Number(grossRaw).toFixed(2));
    const uomPlural = formatUnitLabel(item.unitOfMeasure, grossRounded);

    return {
      netSubtotalText: `${netRounded.toLocaleString()} ${uomPlural}`,
      scrapAllowanceText: `+${ratePercent}% (${scrapRounded.toLocaleString()} ${uomPlural})`,
      grossTotalText: `${grossRounded.toLocaleString()} ${uomPlural}`,
    };
  };

  const syncCalculateDemandWithServer = async (
    nextRate: number,
    itemsToCalc: BomDetailDto[],
    orderQty: number,
  ) => {
    if (itemsToCalc.length === 0 || orderQty <= 0) return;
    try {
      const res = await calculateDemandMutation.mutateAsync({
        orderedQty: orderQty,
        scrapRatePercent: nextRate,
        items: itemsToCalc.map((b) => ({
          materialId: b.materialId,
          netQtyPerUnit: b.netQtyPerUnit,
        })),
      });

      if (res?.data?.calculatedItems) {
        const calcMap = new Map(
          res.data.calculatedItems.map((c) => [`${c.materialId}-${c.netQtyPerUnit}`, c]),
        );
        setBomItems((prev) =>
          prev.map((item) => {
            const serverCalc = calcMap.get(`${item.materialId}-${item.netQtyPerUnit}`);
            return serverCalc !== undefined
              ? {
                  ...item,
                  grossTotalDemand: serverCalc.grossTotalDemand,
                  physicalStock: serverCalc.physicalStock ?? item.physicalStock,
                  allocatedStock: serverCalc.allocatedStock ?? item.allocatedStock,
                  availableStock: serverCalc.availableStock ?? item.availableStock,
                  netDemand: serverCalc.netDemand ?? item.netDemand,
                  isShortage: serverCalc.isShortage ?? item.isShortage,
                }
              : item;
          }),
        );
      }
    } catch (err) {
      if (isAxiosError(err) && err.response?.data?.message) {
        setToastBanner({
          type: "error",
          text: err.response.data.message,
        });
      }
    }
  };

  const handleScrapRateChange = async (valueStr: string) => {
    const parsed = parseFloat(valueStr);
    const nextRate = Number.isNaN(parsed) ? 0 : parsed;
    setScrapRate(nextRate);

    if (nextRate >= 3 && nextRate <= 15 && bomItems.length > 0) {
      await syncCalculateDemandWithServer(nextRate, bomItems, lineItem.orderedQty);
    }
  };

  const handleSelectVisualModel = (
    model: VisualModelOption,
    inheritedRouting?: RoutingStageDto[],
    inheritedDetails?: BomDetailDto[],
    inheritedScrapRate?: number,
  ) => {
    const nextRate = inheritedScrapRate || model.defaultScrapRate || 3.5;
    setScrapRate(nextRate);
    if (inheritedRouting && inheritedRouting.length > 0) {
      setRoutingStages(inheritedRouting);
    }
    if (inheritedDetails && inheritedDetails.length > 0) {
      setBomItems(inheritedDetails);
      syncCalculateDemandWithServer(nextRate, inheritedDetails, lineItem.orderedQty);
    }
    setToastBanner({
      type: "success",
      text: `Đã kế thừa định mức BOM từ mẫu sản phẩm ${model.sku}. Nhấn "Lưu BOM nháp" để lưu.`,
    });
  };

  const handleSaveMaterialItem = (item: BomDetailDto) => {
    setBomItems((prev) => {
      const exists = prev.some((x) => x.bomDetailId === item.bomDetailId);
      const nextList = exists
        ? prev.map((x) => (x.bomDetailId === item.bomDetailId ? item : x))
        : [...prev, item];
      syncCalculateDemandWithServer(scrapRate, nextList, lineItem.orderedQty);
      return nextList;
    });
    setToastBanner({
      type: "success",
      text: editingMaterialItem
        ? `Đã cập nhật định mức vật tư ${item.materialCode}.`
        : `Đã thêm vật tư ${item.materialCode} vào bảng BOM.`,
    });
  };

  const handleDeleteMaterialItem = (bomDetailId: number, materialCode: string) => {
    setBomItems((prev) => prev.filter((x) => x.bomDetailId !== bomDetailId));
    setToastBanner({
      type: "info",
      text: `Đã xóa dòng vật tư ${materialCode} khỏi bảng định mức.`,
    });
  };

  const handleSaveDraftAndReconcile = async () => {
    if (bomItems.length === 0) {
      setToastBanner({
        type: "error",
        text: "Bảng BOM phải có ít nhất 1 nguyên vật liệu trước khi lưu (vui lòng thêm vật tư hoặc Nhập BOM Excel).",
      });
      return;
    }

    const detailsPayload = bomItems.map((item) => ({
      materialId: item.materialId,
      netQtyPerUnit: item.netQtyPerUnit,
      subAssemblyName: item.subAssemblyName || null,
      componentDimensionSpec: item.componentDimensionSpec || null,
      notes: item.notes || null,
    }));

    try {
      const res = await saveBomMutation.mutateAsync({
        bomId: activeBomId,
        createPayload: {
          orderItemId: lineItem.orderItemId,
          productId: lineItem.productId,
          scrapRatePercent: scrapRate,
          details: detailsPayload,
          customRoutingStages: routingStages,
        },
        updatePayload: {
          scrapRatePercent: scrapRate,
          details: detailsPayload,
          customRoutingStages: routingStages,
        },
      });

      if (res?.data) {
        setActiveBomId(res.data.bomId);
        setBomItems(res.data.bomDetails || []);
        setBomStatus(res.data.bomStatusText || "DRAFT");
        setLastSaved(
          new Date().toLocaleTimeString("vi-VN", {
            hour: "2-digit",
            minute: "2-digit",
          }),
        );
      }

      await refetchOrderItemBom();
      onSaved();

      setToastBanner({
        type: "success",
        text:
          res?.message ||
          "Đã lưu BOM và đối soát tồn kho xuống cơ sở dữ liệu thành công!",
      });
    } catch (err) {
      let msg = "Không thể lưu BOM xuống máy chủ. Vui lòng kiểm tra lại dữ liệu.";
      if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({
        type: "error",
        text: msg,
      });
    }
  };

  const handleApproveAndLockBom = async () => {
    if (!activeBomId || activeBomId <= 0) {
      setToastBanner({
        type: "error",
        text: "Vui lòng nhấn 'Lưu BOM nháp' trước khi Phê duyệt & Khóa.",
      });
      return;
    }

    try {
      const res = await approveBomMutation.mutateAsync(activeBomId);
      if (res?.data) {
        setBomStatus(res.data.bomStatusText || "READY_FOR_PRODUCTION");
        setLastSaved(
          new Date().toLocaleTimeString("vi-VN", {
            hour: "2-digit",
            minute: "2-digit",
          }),
        );
      }
      await refetchOrderItemBom();
      onSaved();
      setToastBanner({
        type: "success",
        text:
          res?.message ||
          `Đã phê duyệt và khóa BOM #${activeBomId} sang trạng thái Sẵn sàng sản xuất!`,
      });
    } catch (err) {
      let msg = "Phê duyệt BOM thất bại.";
      if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({
        type: "error",
        text: msg,
      });
    }
  };

  const handleConfirmedStagingBom = async (createdBom: BomDto, message: string) => {
    setActiveBomId(createdBom.bomId);
    setScrapRate(createdBom.scrapRatePercent);
    setBomItems(createdBom.bomDetails || []);
    setBomStatus(createdBom.bomStatusText || "DRAFT");
    setLastSaved("Vừa nhập từ Excel Staging");
    await refetchOrderItemBom();
    onSaved();
    setToastBanner({
      type: "success",
      text: message,
    });
  };

  const getCategoryBadgeClass = (category: string) => {
    const upper = String(category || "").toUpperCase();
    if (upper.includes("LUMBER") || upper.includes("WOOD")) {
      return "bg-amber-50 text-amber-800 border-amber-200";
    }
    if (upper.includes("HARDWARE")) {
      return "bg-blue-50 text-blue-700 border-blue-200";
    }
    if (upper.includes("FINISH") || upper.includes("PAINT")) {
      return "bg-orange-50 text-orange-800 border-orange-200";
    }
    if (upper.includes("PACKAGING")) {
      return "bg-emerald-50 text-emerald-700 border-emerald-200";
    }
    return "bg-slate-100 text-slate-700 border-slate-200";
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-[1px] flex items-center justify-center p-4 overflow-y-auto">
      <div className="w-full max-w-6xl bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden flex flex-col max-h-[92vh]">
        {/* Modal Header */}
        <div className="px-6 py-4 border-b border-slate-200 flex flex-wrap items-center justify-between gap-4 bg-[#F8FAFC]">
          <div>
            <div className="flex items-center gap-2.5">
              <span className="text-xs font-bold uppercase tracking-wider text-blue-600">
                {spaceKey} • DÒNG HÀNG #{lineItem.orderItemId}
              </span>
              <span
                className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 text-xs font-bold uppercase rounded-full border ${
                  isLockedForProduction
                    ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                    : bomStatus === "NOT_CREATED"
                      ? "bg-slate-100 text-slate-700 border-slate-200"
                      : "bg-amber-50 text-amber-800 border-amber-200"
                }`}
              >
                <span
                  className={`w-1.5 h-1.5 rounded-full ${
                    isLockedForProduction
                      ? "bg-emerald-500"
                      : bomStatus === "NOT_CREATED"
                        ? "bg-slate-500"
                        : "bg-amber-500"
                  }`}
                />
                {activeBomId ? `BOM #${activeBomId} • ` : ""}
                {bomStatusLabel(bomStatus)}
              </span>
            </div>
            <h2 className="text-lg font-extrabold text-slate-900 mt-1">
              Tùy chỉnh BOM: {lineItem.productSku} — {lineItem.productName} (SL đặt:{" "}
              {lineItem.orderedQty} cái)
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Quy cách: {lineItem.dimensionsSpec} | Gỗ: {lineItem.woodSpecies} | Hoàn thiện:{" "}
              {lineItem.finishSpec} | Lưu lần cuối: {lastSaved}
            </p>
          </div>

          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-2xl font-bold leading-none px-2 py-1 cursor-pointer"
          >
            ×
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 overflow-y-auto space-y-5 flex-1">
          {toastBanner && (
            <div
              className={`px-4 py-3 rounded-lg border text-sm font-medium flex items-center justify-between shadow-2xs ${
                toastBanner.type === "success"
                  ? "bg-emerald-50 border-emerald-200 text-emerald-800"
                  : toastBanner.type === "error"
                    ? "bg-rose-50 border-rose-200 text-rose-700"
                    : "bg-blue-50 border-blue-200 text-blue-800"
              }`}
            >
              <span>{toastBanner.text}</span>
              <button
                type="button"
                onClick={() => setToastBanner(null)}
                className="text-slate-400 hover:text-slate-600 ml-3 font-bold text-base leading-none cursor-pointer"
              >
                ×
              </button>
            </div>
          )}

          {/* Scrap Rate & Action Toolbar */}
          <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 flex flex-col lg:flex-row lg:items-center justify-between gap-4">
            <div className="flex flex-wrap items-center gap-3">
              <label
                htmlFor="modalOwnerScrapRate"
                className="text-sm font-semibold text-slate-700"
              >
                Tỷ lệ hao hụt cho phép (3% - 15%):
              </label>
              <div className="flex items-center gap-1.5">
                <input
                  id="modalOwnerScrapRate"
                  type="number"
                  step="0.1"
                  min="3"
                  max="15"
                  value={scrapRate}
                  onChange={(e) => handleScrapRateChange(e.target.value)}
                  className="w-20 px-3 py-1.5 text-sm font-bold text-slate-900 bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600 text-center"
                />
                <span className="text-sm font-semibold text-slate-500">%</span>
              </div>
              <code className="font-mono text-xs text-blue-700 bg-blue-50 px-2.5 py-1 rounded border border-blue-200">
                Tổng nhu cầu = (Định mức 1 SP × {lineItem.orderedQty}) × (1 + {scrapRate}%)
              </code>
            </div>

            <div className="flex flex-wrap items-center gap-2.5">
              <button
                type="button"
                onClick={() => setShowStagingModal(true)}
                className="px-3.5 py-2 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg transition cursor-pointer"
              >
                Nhập BOM Excel
              </button>
              <button
                type="button"
                onClick={() => setShowVisualCatalogModal(true)}
                className="px-3.5 py-2 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg transition cursor-pointer"
              >
                Kế thừa từ danh mục
              </button>
              <button
                type="button"
                onClick={() => {
                  setEditingMaterialItem(null);
                  setShowMaterialModal(true);
                }}
                className="px-3.5 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold rounded-lg shadow-2xs transition cursor-pointer"
              >
                + Thêm vật tư
              </button>
            </div>
          </div>

          {/* BOM Material Breakdown Table */}
          <div className="border border-slate-200 rounded-xl overflow-hidden">
            <div className="overflow-x-auto bg-white">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                    <th className="py-3 px-4 min-w-[130px]">Mã vật tư</th>
                    <th className="py-3 px-3 min-w-[110px]">Nhóm</th>
                    <th className="py-3 px-4 min-w-[240px]">Tên vật tư &amp; Quy cách chi tiết</th>
                    <th className="py-3 px-3 min-w-[70px]">ĐVT</th>
                    <th className="py-3 px-3 min-w-[110px]">Định mức / SP</th>
                    <th className="py-3 px-3 min-w-[140px]">
                      Tổng định mức ({lineItem.orderedQty} cái)
                    </th>
                    <th className="py-3 px-3 min-w-[130px]">Hao hụt</th>
                    <th className="py-3 px-4 min-w-[170px]">Tổng nhu cầu &amp; Tồn kho</th>
                    <th className="py-3 px-4 text-right min-w-[120px]">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 text-sm bg-white">
                  {isLoadingBom ? (
                    <tr>
                      <td colSpan={9} className="py-10 text-center text-slate-500">
                        Đang tải dữ liệu BOM của sản phẩm từ máy chủ...
                      </td>
                    </tr>
                  ) : bomItems.length === 0 ? (
                    <tr>
                      <td colSpan={9} className="py-10 px-6 text-center text-rose-600 font-medium">
                        Sản phẩm <strong>{lineItem.productSku}</strong> (Dòng hàng{" "}
                        <strong>#{lineItem.orderItemId}</strong>) chưa có dữ liệu vật tư BOM nào. Hãy nhấn{" "}
                        <strong>Nhập BOM Excel</strong>, <strong>Kế thừa từ danh mục</strong> hoặc{" "}
                        <strong>+ Thêm vật tư</strong> để thiết lập.
                      </td>
                    </tr>
                  ) : (
                    bomItems.map((item) => {
                      const metrics = computeRowMetrics(
                        item,
                        lineItem.orderedQty,
                        scrapRate,
                      );
                      return (
                        <tr
                          key={item.bomDetailId}
                          className="hover:bg-slate-50/90 transition-colors"
                        >
                          <td className="py-3.5 px-4 font-mono text-sm font-bold text-slate-800 whitespace-nowrap">
                            {item.materialCode}
                          </td>
                          <td className="py-3.5 px-3 whitespace-nowrap">
                            <span
                              className={`inline-block px-2 py-0.5 text-xs font-bold uppercase rounded border ${getCategoryBadgeClass(
                                item.materialGroup,
                              )}`}
                            >
                              {item.materialGroup}
                            </span>
                          </td>
                          <td className="py-3.5 px-4">
                            <div className="font-semibold text-slate-900">
                              {item.materialName}
                            </div>
                            {(item.subAssemblyName || item.componentDimensionSpec) && (
                              <div className="flex flex-wrap items-center gap-1.5 mt-1">
                                {item.subAssemblyName && (
                                  <span className="px-2 py-0.5 text-xs font-semibold rounded bg-amber-50 text-amber-800 border border-amber-200">
                                    {item.subAssemblyName}
                                  </span>
                                )}
                                {item.componentDimensionSpec && (
                                  <span className="font-mono text-xs font-semibold text-blue-700 bg-blue-50 px-2 py-0.5 rounded border border-blue-200">
                                    {item.componentDimensionSpec}
                                  </span>
                                )}
                              </div>
                            )}
                            {(item.dimensionSpec || item.notes) && (
                              <div className="text-xs text-slate-500 mt-1">
                                {item.dimensionSpec}
                                {item.dimensionSpec && item.notes ? " • " : ""}
                                {item.notes}
                              </div>
                            )}
                          </td>
                          <td className="py-3.5 px-3 text-slate-600 whitespace-nowrap">
                            {item.unitOfMeasure}
                          </td>
                          <td className="py-3.5 px-3 font-bold text-slate-900 whitespace-nowrap">
                            {item.netQtyPerUnit}
                          </td>
                          <td className="py-3.5 px-3 text-slate-700 whitespace-nowrap">
                            {metrics.netSubtotalText}
                          </td>
                          <td className="py-3.5 px-3 text-slate-600 whitespace-nowrap">
                            {metrics.scrapAllowanceText}
                          </td>
                          <td className="py-3.5 px-4 whitespace-nowrap">
                            <div className="font-extrabold text-blue-700">
                              {metrics.grossTotalText}
                            </div>
                            {item.availableStock !== undefined && (
                              <div className="mt-1 flex items-center gap-1.5 text-xs">
                                <span className="text-slate-500">
                                  Khả dụng:{" "}
                                  <strong className="text-slate-800">
                                    {item.availableStock}
                                  </strong>
                                </span>
                                {item.isShortage ? (
                                  <span className="px-2 py-0.5 rounded bg-rose-50 text-rose-700 border border-rose-200 font-bold">
                                    Thiếu: {item.netDemand}
                                  </span>
                                ) : (
                                  <span className="px-2 py-0.5 rounded bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold">
                                    Đủ tồn
                                  </span>
                                )}
                              </div>
                            )}
                          </td>
                          <td className="py-3.5 px-4 text-right whitespace-nowrap">
                            <div className="inline-flex items-center gap-1.5">
                              <button
                                type="button"
                                onClick={() => {
                                  setEditingMaterialItem(item);
                                  setShowMaterialModal(true);
                                }}
                                className="px-2.5 py-1 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg transition cursor-pointer"
                              >
                                Sửa
                              </button>
                              <button
                                type="button"
                                onClick={() =>
                                  handleDeleteMaterialItem(
                                    item.bomDetailId,
                                    item.materialCode,
                                  )
                                }
                                className="px-2.5 py-1 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 text-xs font-bold rounded-lg transition cursor-pointer"
                              >
                                Xóa
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        {/* Modal Footer */}
        <div className="px-6 py-4 border-t border-slate-200 bg-[#F8FAFC] flex flex-wrap items-center justify-between gap-3">
          <div className="text-xs text-slate-500">
            Tổng số dòng vật tư: <strong className="text-slate-900">{bomItems.length}</strong>
            {calculateDemandMutation.isPending && (
              <span className="ml-2 text-blue-600 font-medium">
                • Đang tính toán nhu cầu...
              </span>
            )}
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-100 transition cursor-pointer"
            >
              Đóng
            </button>

            <button
              type="button"
              onClick={handleSaveDraftAndReconcile}
              disabled={saveBomMutation.isPending}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-50 cursor-pointer"
            >
              {saveBomMutation.isPending
                ? "Đang lưu..."
                : activeBomId
                  ? "Lưu bản nháp & Đối chiếu"
                  : "Lưu BOM nháp mới"}
            </button>

            {activeBomId && !isLockedForProduction && (
              <button
                type="button"
                onClick={handleApproveAndLockBom}
                disabled={approveBomMutation.isPending}
                className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-50 cursor-pointer"
              >
                {approveBomMutation.isPending ? "Đang duyệt..." : "Duyệt & Khóa BOM"}
              </button>
            )}
          </div>
        </div>
      </div>

      <VisualProductCatalogModal
        isOpen={showVisualCatalogModal}
        currentModelKey={(lineItem.productSku || "").toLowerCase()}
        onClose={() => setShowVisualCatalogModal(false)}
        onSelectModel={handleSelectVisualModel}
      />

      <MaterialItemModal
        isOpen={showMaterialModal}
        editingItem={editingMaterialItem}
        onClose={() => {
          setShowMaterialModal(false);
          setEditingMaterialItem(null);
        }}
        onSave={handleSaveMaterialItem}
      />

      <BomStagingModal
        isOpen={showStagingModal}
        orderItemId={lineItem.orderItemId}
        productId={lineItem.productId}
        productSku={lineItem.productSku}
        productName={lineItem.productName}
        existingBomId={activeBomId}
        initialScrapRate={scrapRate}
        onClose={() => setShowStagingModal(false)}
        onConfirmedBom={handleConfirmedStagingBom}
      />
    </div>
  );
}
