import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useWmms } from "../../auth/hooks/WmmsContext";
import { orderIntakeApi } from "../../../services/wmmsApi";
import { bomApi } from "../api/bomApi";
import {
  DEFAULT_BENCHMARK_BOM,
  DEFAULT_BENCHMARK_ROUTING,
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
} from "../components/VisualProductCatalogModal";
import DynamicRoutingModal from "../components/DynamicRoutingModal";
import MaterialItemModal from "../components/MaterialItemModal";
import BomStagingModal from "../components/BomStagingModal";
import { bomStatusLabel } from "../../../lib/statusLabels";

export default function BomBuilderPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { orderSpaces, currentSpaceKey, setCurrentSpaceKey, setOrderSpaces } = useWmms();

  // State danh sách Line Items thực tế của Order Space đang chọn
  const [spaceLineItems, setSpaceLineItems] = useState<OrderSpaceLineItemForBom[]>([]);
  const [spaceCustomerName, setSpaceCustomerName] = useState<string>("");
  const [spacePoNumber, setSpacePoNumber] = useState<string>("");
  const [isLoadingSpace, setIsLoadingSpace] = useState<boolean>(false);

  // OrderItemId đang được chọn để tạo/chỉnh sửa BOM
  const initialOrderItemParam = Number(searchParams.get("orderItemId")) || 0;
  const [selectedOrderItemId, setSelectedOrderItemId] = useState<number>(
    initialOrderItemParam || 1,
  );

  // Truy vấn BOM thực tế của OrderItem từ Backend (GET /api/v1/bom/order-item/{orderItemId})
  const {
    data: serverBom,
    isLoading: isLoadingBom,
    refetch: refetchOrderItemBom,
  } = useOrderItemBom(selectedOrderItemId);

  const calculateDemandMutation = useCalculateDemand();
  const saveBomMutation = useSaveBomDraft();
  const approveBomMutation = useApproveBom();

  // State sản phẩm mục tiêu trên BOM Builder
  const [activeBomId, setActiveBomId] = useState<number | null>(null);
  const [targetProduct, setTargetProduct] = useState({
    productId: DEFAULT_BENCHMARK_BOM.productId,
    modelKey: "chair-dining-oak-01",
    sku: DEFAULT_BENCHMARK_BOM.productSku,
    productName: DEFAULT_BENCHMARK_BOM.productName,
    orderQty: DEFAULT_BENCHMARK_BOM.orderedQty,
    dimensions: DEFAULT_BENCHMARK_BOM.dimensionsCm,
    finishSpec: "Óc chó tự nhiên",
    fscRequired: true,
    cadDrawing: "CAD_Chair_880504.pdf",
    bomStatus: "DRAFT",
    lastSaved: "Chưa lưu",
  });

  // State quy trình công đoạn (Workstation Sequence)
  const [routingStages, setRoutingStages] = useState<RoutingStageDto[]>(
    DEFAULT_BENCHMARK_ROUTING,
  );

  // State tỷ lệ hao hụt gỗ (Owner Loss Allowance %)
  const [scrapRate, setScrapRate] = useState<number>(3.5);

  // State danh sách vật tư BOM
  const [bomItems, setBomItems] = useState<BomDetailDto[]>([]);

  // State các Modal
  const [showVisualCatalogModal, setShowVisualCatalogModal] = useState(false);
  const [showRoutingModal, setShowRoutingModal] = useState(false);
  const [showMaterialModal, setShowMaterialModal] = useState(false);
  const [showStagingModal, setShowStagingModal] = useState(false);
  const [editingMaterialItem, setEditingMaterialItem] = useState<BomDetailDto | null>(null);

  // Thông báo trạng thái
  const [toastBanner, setToastBanner] = useState<{
    type: "success" | "error" | "info";
    text: string;
  } | null>(null);

  const isLockedForProduction =
    targetProduct.bomStatus === "READY_FOR_PRODUCTION" ||
    targetProduct.bomStatus === "LOCKED";

  // 1. Đồng bộ danh sách Order Spaces từ Backend khi vào trang
  useEffect(() => {
    const spaceParam = searchParams.get("spaceKey");
    if (spaceParam && spaceParam !== currentSpaceKey) {
      setCurrentSpaceKey(spaceParam);
    }

    orderIntakeApi
      .getSalesOrders({ page: 1, pageSize: 50 })
      .then((res) => {
        const items = res?.data?.orders || res?.data?.items || [];
        if (Array.isArray(items) && items.length > 0) {
          const mappedSpaces = items.map((o: Record<string, unknown>) => ({
            spaceKey: String(o.orderSpaceKey || o.spaceKey || `SPACE-${o.poNumber}`),
            spaceLabel: `${String(o.orderSpaceKey || o.spaceKey || `SPACE-${o.poNumber}`)} (${String(o.customerName || "")})`,
            customerName: String(o.customerName || ""),
            poNumber: String(o.poNumber || ""),
            orderDate: o.orderDate ? String(o.orderDate).slice(0, 10) : "",
            etdDate: o.etdDate ? String(o.etdDate).slice(0, 10) : "",
            daysRemaining: Number(o.daysRemaining ?? 30),
            isUrgentEtd: Boolean(o.isUrgentEtd),
            totalOrderedUnits: Number(o.totalOrderedUnits ?? 0),
            lineItemsVolumeSummary: String(o.lineItemsVolumeSummary || "Đơn hàng xuất khẩu"),
            lifecycleStatusBadge: String(o.lifecycleStatusBadge || o.orderStatus || "CONFIRMED"),
            overallProgressPct: Number(o.overallProgressPct ?? o.bomReadinessPct ?? 0),
          }));
          setOrderSpaces(mappedSpaces);

          const targetKey = spaceParam || currentSpaceKey;
          const exists = mappedSpaces.some((s) => s.spaceKey === targetKey);
          if (!exists && mappedSpaces[0]?.spaceKey) {
            setCurrentSpaceKey(mappedSpaces[0].spaceKey);
          }
        }
      })
      .catch(() => {
        // Giữ nguyên danh sách Order Space trong context nếu mất kết nối
      });
  }, []);

  // 2. Tải danh sách Line Items thực tế của Order Space đang chọn (GET /api/v1/orders/spaces/{idOrKey})
  const loadOrderSpaceLineItems = async (spaceKey: string) => {
    if (!spaceKey) return;
    setIsLoadingSpace(true);
    try {
      const res = await orderIntakeApi.getOrderSpaceDetail(spaceKey);
      const detail = res?.data;
      if (detail) {
        setSpaceCustomerName(detail.customerName || "");
        setSpacePoNumber(detail.poNumber || "");
        const items: OrderSpaceLineItemForBom[] = Array.isArray(detail.lineItems)
          ? detail.lineItems
          : [];
        setSpaceLineItems(items);

        if (items.length > 0) {
          const urlItemId = Number(searchParams.get("orderItemId")) || 0;
          const matchedUrlItem = items.find((x) => x.orderItemId === urlItemId);
          const matchedCurrentItem = items.find(
            (x) => x.orderItemId === selectedOrderItemId,
          );
          const chosenItem = matchedUrlItem || matchedCurrentItem || items[0];
          setSelectedOrderItemId(chosenItem.orderItemId);
        }
      }
    } catch {
      setSpaceLineItems([]);
    } finally {
      setIsLoadingSpace(false);
    }
  };

  useEffect(() => {
    if (currentSpaceKey) {
      loadOrderSpaceLineItems(currentSpaceKey);
    }
  }, [currentSpaceKey]);

  // 3. Khi `serverBom` hoặc `selectedOrderItemId` thay đổi: nạp dữ liệu BOM từ Server hoặc kế thừa từ Product
  useEffect(() => {
    const activeLineItem = spaceLineItems.find(
      (x) => x.orderItemId === selectedOrderItemId,
    );

    if (serverBom) {
      setActiveBomId(serverBom.bomId);
      setTargetProduct({
        productId: serverBom.productId,
        modelKey: (serverBom.productSku || "").toLowerCase(),
        sku: serverBom.productSku,
        productName: serverBom.productName,
        orderQty: serverBom.orderedQty,
        dimensions:
          serverBom.dimensionsCm || activeLineItem?.dimensionsSpec || "Quy cách chuẩn",
        finishSpec:
          activeLineItem?.finishSpec || activeLineItem?.woodSpecies || "Hoàn thiện chuẩn",
        fscRequired: activeLineItem?.fscCertifiedReq ?? true,
        cadDrawing:
          activeLineItem?.attachedCadFileName || `CAD_${serverBom.productSku}.pdf`,
        bomStatus: serverBom.bomStatusText || String(serverBom.bomStatus || "DRAFT"),
        lastSaved: serverBom.updatedAt
          ? new Date(serverBom.updatedAt).toLocaleString("en-GB")
          : serverBom.createdAt
            ? new Date(serverBom.createdAt).toLocaleString("en-GB")
            : "Đã đồng bộ với máy chủ",
      });
      setScrapRate(serverBom.scrapRatePercent || 3.5);
      setBomItems(serverBom.bomDetails || []);
      if (serverBom.routingStages && serverBom.routingStages.length > 0) {
        setRoutingStages(serverBom.routingStages);
      }
      return;
    }

    // Trường hợp OrderItem chưa có BOM trong DB (serverBom === null)
    if (!isLoadingBom && selectedOrderItemId > 0) {
      setActiveBomId(null);
      const prodId = activeLineItem?.productId || 1;
      const prodSku = activeLineItem?.productSku || DEFAULT_BENCHMARK_BOM.productSku;
      const prodName = activeLineItem?.productName || DEFAULT_BENCHMARK_BOM.productName;
      const ordQty = activeLineItem?.orderedQty || DEFAULT_BENCHMARK_BOM.orderedQty;

      setTargetProduct({
        productId: prodId,
        modelKey: prodSku.toLowerCase(),
        sku: prodSku,
        productName: prodName,
        orderQty: ordQty,
        dimensions: activeLineItem?.dimensionsSpec || DEFAULT_BENCHMARK_BOM.dimensionsCm,
        finishSpec: activeLineItem?.finishSpec || activeLineItem?.woodSpecies || "Hoàn thiện chuẩn",
        fscRequired: activeLineItem?.fscCertifiedReq ?? true,
        cadDrawing: activeLineItem?.attachedCadFileName || `CAD_${prodSku}.pdf`,
        bomStatus: "NOT_CREATED",
        lastSaved: "Chưa tạo trong CSDL",
      });

      // Tự động kiểm tra xem sản phẩm này đã có BOM lịch sử để kế thừa không (GET /api/v1/bom/check-product/{productId})
      bomApi
        .checkProductBom(prodId)
        .then(async (res) => {
          const inherited = res?.data;
          if (inherited?.hasExistingBom && inherited.details?.length > 0) {
            setScrapRate(inherited.scrapRatePercent || 3.5);
            setBomItems(inherited.details);
            if (inherited.routingStages?.length > 0) {
              setRoutingStages(inherited.routingStages);
            }
            setToastBanner({
              type: "info",
              text:
                res.message ||
                `Order Item #${selectedOrderItemId} chưa có BOM riêng. Hệ thống đã tự động gợi ý định mức kế thừa từ BOM #${inherited.sourceBomId} của sản phẩm ${prodSku}. Nhấn "Save Draft" để lưu.`,
            });
          } else {
            // Chưa có BOM lịch sử -> tải Default Routing của sản phẩm từ DB
            setBomItems([]);
            setScrapRate(3.5);
            try {
              const routeRes = await bomApi.getDefaultRouting(prodId);
              if (routeRes?.data?.length) {
                setRoutingStages(routeRes.data);
              } else {
                setRoutingStages(DEFAULT_BENCHMARK_ROUTING);
              }
            } catch {
              setRoutingStages(DEFAULT_BENCHMARK_ROUTING);
            }
          }
        })
        .catch(() => {
          // Fallback khi server không phản hồi
          setBomItems(DEFAULT_BENCHMARK_BOM.bomDetails);
          setRoutingStages(DEFAULT_BENCHMARK_ROUTING);
        });
    }
  }, [serverBom, isLoadingBom, selectedOrderItemId, spaceLineItems]);

  // Chuyển đổi giữa các Line Items trong cùng Order Space
  const handleSelectOrderItem = (item: OrderSpaceLineItemForBom) => {
    setSelectedOrderItemId(item.orderItemId);
    setToastBanner(null);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("orderItemId", String(item.orderItemId));
      if (currentSpaceKey) next.set("spaceKey", currentSpaceKey);
      return next;
    });
  };

  // Định dạng đơn vị hiển thị
  const formatUnitLabel = (uom: string, value: number) => {
    if (uom === "Sheet" && value !== 1) return "Sheets";
    if (uom === "Box" && value !== 1) return "Boxes";
    return uom;
  };

  // Tính toán Net Subtotal, Scrap Allowance và Gross Total Demand cho từng dòng vật tư
  const computeRowMetrics = (item: BomDetailDto, orderQty: number, ratePercent: number) => {
    const uomUpper = (item.unitOfMeasure || "").toUpperCase();
    const isDecimalUnit =
      uomUpper === "M3" || uomUpper === "M³" || uomUpper === "LITER" || uomUpper === "KG";
    const netSubtotal = item.netQtyPerUnit * orderQty;
    const scrapRaw = netSubtotal * (ratePercent / 100);
    // Ưu tiên grossTotalDemand trả về từ Backend nếu khớp, hoặc tính theo công thức chuẩn
    const calculatedGross = Number((netSubtotal * (1 + ratePercent / 100)).toFixed(4));
    const grossRaw =
      item.grossTotalDemand && item.grossTotalDemand > 0
        ? item.grossTotalDemand
        : calculatedGross;

    if (isDecimalUnit) {
      const netFormatted = netSubtotal.toFixed(3);
      const scrapFormatted = scrapRaw.toFixed(3);
      const grossFormatted = Number(grossRaw).toFixed(3);
      return {
        netSubtotalText: `${netFormatted} ${item.unitOfMeasure}`,
        scrapAllowanceText: `+${ratePercent}% (${scrapFormatted} ${item.unitOfMeasure})`,
        grossTotalText: `${grossFormatted} ${item.unitOfMeasure}`,
        grossNumeric: Number(grossFormatted),
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
      grossNumeric: grossRounded,
    };
  };

  // Gọi API Backend POST /api/v1/bom/calculate-demand để đồng bộ Gross Total Demand & Tồn kho từ Server
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

  // Khi thay đổi Owner Loss Allowance % (Field #5)
  const handleScrapRateChange = async (valueStr: string) => {
    const parsed = parseFloat(valueStr);
    const nextRate = Number.isNaN(parsed) ? 0 : parsed;
    setScrapRate(nextRate);

    if (nextRate >= 3 && nextRate <= 15 && bomItems.length > 0) {
      await syncCalculateDemandWithServer(nextRate, bomItems, targetProduct.orderQty);
    }
  };

  // Áp dụng mẫu sản phẩm từ Visual Product Catalog Modal
  const handleSelectVisualModel = (
    model: VisualModelOption,
    inheritedRouting?: RoutingStageDto[],
    inheritedDetails?: BomDetailDto[],
    inheritedScrapRate?: number,
  ) => {
    const nextRate = inheritedScrapRate || model.defaultScrapRate || 3.5;
    setTargetProduct((prev) => ({
      ...prev,
      productId: model.id,
      modelKey: model.key,
      sku: model.sku,
      productName: model.productName,
      dimensions: model.dimensions || prev.dimensions,
      finishSpec: model.finishSpec || prev.finishSpec,
      cadDrawing: model.cadDrawing || prev.cadDrawing,
    }));
    setScrapRate(nextRate);
    if (inheritedRouting && inheritedRouting.length > 0) {
      setRoutingStages(inheritedRouting);
    }
    if (inheritedDetails && inheritedDetails.length > 0) {
      setBomItems(inheritedDetails);
      syncCalculateDemandWithServer(nextRate, inheritedDetails, targetProduct.orderQty);
    }
    setToastBanner({
      type: "success",
      text: `Đã kế thừa quy trình Routing và định mức BOM từ mẫu sản phẩm ${model.sku} trên Server.`,
    });
  };

  // Lưu cấu hình chuỗi công đoạn từ Dynamic Routing Modal
  const handleSaveRoutingSequence = (updatedStages: RoutingStageDto[]) => {
    setRoutingStages(updatedStages);
    setToastBanner({
      type: "success",
      text: "Đã cập nhật trình tự công đoạn sản xuất (Dynamic Routing Sequence). Nhấn 'Save Draft' để lưu xuống Database.",
    });
  };

  // Thêm hoặc cập nhật dòng vật tư BOM
  const handleSaveMaterialItem = (item: BomDetailDto) => {
    setBomItems((prev) => {
      const exists = prev.some((x) => x.bomDetailId === item.bomDetailId);
      const nextList = exists
        ? prev.map((x) => (x.bomDetailId === item.bomDetailId ? item : x))
        : [...prev, item];
      syncCalculateDemandWithServer(scrapRate, nextList, targetProduct.orderQty);
      return nextList;
    });
    setToastBanner({
      type: "success",
      text: editingMaterialItem
        ? `Đã cập nhật định mức vật tư ${item.materialCode}.`
        : `Đã thêm vật tư ${item.materialCode} vào bảng BOM.`,
    });
  };

  // Xóa 1 dòng vật tư khỏi bảng BOM
  const handleDeleteMaterialItem = (bomDetailId: number, materialCode: string) => {
    setBomItems((prev) => prev.filter((x) => x.bomDetailId !== bomDetailId));
    setToastBanner({
      type: "info",
      text: `Đã xóa dòng vật tư ${materialCode} khỏi bảng định mức.`,
    });
  };

  // Lưu nháp BOM xuống Backend (POST /api/v1/bom hoặc PUT /api/v1/bom/{bomId})
  const handleSaveDraftAndReconcile = async () => {
    if (bomItems.length === 0) {
      setToastBanner({
        type: "error",
        text: "Bảng BOM phải có ít nhất 1 nguyên vật liệu trước khi lưu (vui lòng thêm vật tư hoặc Import Excel Staging).",
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
          orderItemId: selectedOrderItemId,
          productId: targetProduct.productId,
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
        if (res.data.routingStages?.length) {
          setRoutingStages(res.data.routingStages);
        }
        const nowTime = new Date().toLocaleTimeString("en-GB", {
          hour: "2-digit",
          minute: "2-digit",
        });
        setTargetProduct((prev) => ({
          ...prev,
          bomStatus: res.data.bomStatusText || "DRAFT",
          lastSaved: `Hôm nay ${nowTime}`,
        }));
      }

      await refetchOrderItemBom();
      if (currentSpaceKey) {
        await loadOrderSpaceLineItems(currentSpaceKey);
      }

      setToastBanner({
        type: "success",
        text:
          res?.message ||
          "Đã lưu BOM kèm quy trình sản xuất và đối soát tồn kho xuống Database thành công!",
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

  // Phê duyệt & Chốt khóa BOM sang READY_FOR_PRODUCTION (PATCH /api/v1/bom/{bomId}/approve)
  const handleApproveAndLockBom = async () => {
    if (!activeBomId || activeBomId <= 0) {
      setToastBanner({
        type: "error",
        text: "Vui lòng nhấn 'Save Draft' để lưu BOM vào hệ thống trước khi Phê duyệt & Khóa (Approve).",
      });
      return;
    }

    try {
      const res = await approveBomMutation.mutateAsync(activeBomId);
      if (res?.data) {
        setTargetProduct((prev) => ({
          ...prev,
          bomStatus: res.data.bomStatusText || "READY_FOR_PRODUCTION",
          lastSaved: new Date().toLocaleTimeString("en-GB", {
            hour: "2-digit",
            minute: "2-digit",
          }),
        }));
      }
      await refetchOrderItemBom();
      if (currentSpaceKey) {
        await loadOrderSpaceLineItems(currentSpaceKey);
      }
      setToastBanner({
        type: "success",
        text:
          res?.message ||
          `Đã phê duyệt và khóa BOM #${activeBomId} sang trạng thái READY_FOR_PRODUCTION!`,
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

  // Khi hoàn tất Confirm BOM Staging từ Modal
  const handleConfirmedStagingBom = async (createdBom: BomDto, message: string) => {
    setActiveBomId(createdBom.bomId);
    setScrapRate(createdBom.scrapRatePercent);
    setBomItems(createdBom.bomDetails || []);
    if (createdBom.routingStages?.length) {
      setRoutingStages(createdBom.routingStages);
    }
    setTargetProduct((prev) => ({
      ...prev,
      bomStatus: createdBom.bomStatusText || "DRAFT",
      lastSaved: "Vừa nhập từ Excel Staging",
    }));
    await refetchOrderItemBom();
    if (currentSpaceKey) {
      await loadOrderSpaceLineItems(currentSpaceKey);
    }
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
    <WmmsAppShell
      customTopLeft={
        <button
          type="button"
          onClick={() => navigate("/orders")}
          className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-sm font-semibold text-slate-700 bg-white border border-slate-300 hover:bg-slate-50 transition shadow-2xs cursor-pointer"
        >
          <svg className="w-4 h-4 text-slate-500" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
          </svg>
          <span>Danh sách đơn hàng</span>
        </button>
      }
    >
      <div className="max-w-[1400px] mx-auto space-y-6">
        {/* Feedback Banner */}
        {toastBanner && (
          <div
            className={`px-4 py-3 rounded-lg border text-sm font-medium flex items-center justify-between shadow-xs ${
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

        {/* Header + Action Buttons */}
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
              Định mức BOM &amp; Quy trình sản xuất động
            </h1>
            <p className="text-sm text-slate-600 mt-1">
              Bóc tách định mức vật tư, nhập BOM Excel qua Staging, kế thừa mẫu sản phẩm và khóa BOM đưa vào sản xuất.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5 shrink-0">
            <button
              type="button"
              onClick={() => setShowStagingModal(true)}
              className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
            >
              <svg className="w-4 h-4 text-slate-500" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
              </svg>
              <span>Nhập BOM Excel</span>
            </button>

            <button
              type="button"
              onClick={() => setShowVisualCatalogModal(true)}
              className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
            >
              <svg className="w-4 h-4 text-slate-500" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
              </svg>
              <span>Kế thừa từ danh mục</span>
            </button>

            <button
              type="button"
              onClick={() => setShowRoutingModal(true)}
              className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
            >
              <svg className="w-4 h-4 text-slate-500" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
              </svg>
              <span>Cấu hình quy trình</span>
            </button>

            <button
              type="button"
              onClick={handleSaveDraftAndReconcile}
              disabled={saveBomMutation.isPending}
              className="px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-50 cursor-pointer"
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
                className="px-4 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-50 cursor-pointer"
              >
                {approveBomMutation.isPending ? "Đang duyệt..." : "Duyệt & Khóa BOM"}
              </button>
            )}
          </div>
        </div>

        {/* Order Space & Line Items Selector Card */}
        <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-3.5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                  Order Space:
                </span>
                <select
                  value={currentSpaceKey}
                  onChange={(e) => setCurrentSpaceKey(e.target.value)}
                  className="px-3 py-1.5 text-sm font-semibold bg-slate-50 border border-slate-300 rounded-lg text-slate-900 focus:outline-none focus:ring-2 focus:ring-blue-600 cursor-pointer"
                >
                  {orderSpaces.map((sp) => (
                    <option key={sp.spaceKey} value={sp.spaceKey}>
                      {sp.spaceLabel || `${sp.spaceKey} (${sp.customerName})`}
                    </option>
                  ))}
                </select>
              </div>
              {spacePoNumber && (
                <span className="font-medium text-slate-700">
                  PO: <strong className="font-mono text-slate-900">{spacePoNumber}</strong>
                </span>
              )}
              {spaceCustomerName && (
                <span className="text-slate-600">• Khách hàng: <strong className="text-slate-800">{spaceCustomerName}</strong></span>
              )}
            </div>
            <span className="text-xs text-slate-500">
              Chọn dòng sản phẩm bên dưới để thiết lập hoặc chỉnh sửa BOM:
            </span>
          </div>

          {isLoadingSpace ? (
            <div className="text-sm text-slate-500 py-1">
              Đang tải các dòng sản phẩm của Order Space...
            </div>
          ) : spaceLineItems.length > 0 ? (
            <div className="flex flex-wrap items-center gap-2.5">
              {spaceLineItems.map((li) => {
                const isSelected = li.orderItemId === selectedOrderItemId;
                const isReady =
                  li.itemStatus === "READY_PROD" ||
                  (li.bomLifecycleBadge || "").includes("LOCKED") ||
                  (li.bomLifecycleBadge || "").includes("READY");
                const hasDraft = Boolean(li.bomId) && !isReady;
                return (
                  <button
                    key={li.orderItemId}
                    type="button"
                    onClick={() => handleSelectOrderItem(li)}
                    className={`px-4 py-2.5 rounded-lg border text-left transition flex items-center gap-3 cursor-pointer ${
                      isSelected
                        ? "border-2 border-blue-600 bg-blue-50/40 shadow-2xs"
                        : "border-slate-200 bg-white hover:bg-slate-50"
                    }`}
                  >
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-mono text-xs font-bold text-slate-900">
                          #{li.orderItemId} • {li.productSku}
                        </span>
                        <span
                          className={`px-2 py-0.5 text-[10px] font-bold uppercase rounded border ${
                            isReady
                              ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                              : hasDraft
                                ? "bg-amber-50 text-amber-800 border-amber-200"
                                : "bg-slate-100 text-slate-600 border-slate-200"
                          }`}
                        >
                          {isReady ? "SẴN SÀNG SX" : hasDraft ? `BOM #${li.bomId} NHÁP` : "CHƯA CÓ BOM"}
                        </span>
                      </div>
                      <div className="text-xs text-slate-600 mt-0.5">
                        {li.productName} ({li.orderedQty} cái)
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>
          ) : (
            <div className="text-sm text-slate-600">
              Dòng hàng đang chọn: <strong className="font-mono text-slate-900">#{selectedOrderItemId}</strong>
            </div>
          )}
        </div>

        {/* Active Target Product Summary Card */}
        <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <div className="text-xs font-bold uppercase tracking-wider text-blue-600">
              SẢN PHẨM ĐANG THIẾT LẬP BOM (DÒNG HÀNG #{selectedOrderItemId} • MÃ SẢN PHẨM #{targetProduct.productId})
            </div>
            <div className="text-base font-extrabold text-slate-900 mt-1">
              SKU: {targetProduct.sku} — {targetProduct.productName} (Số lượng đặt:{" "}
              {targetProduct.orderQty} cái)
            </div>
            <div className="text-sm text-slate-600 mt-1">
              Kích thước: {targetProduct.dimensions} | Quy cách: {targetProduct.finishSpec} |{" "}
              {targetProduct.fscRequired ? "Yêu cầu gỗ FSC 100%" : "Gỗ tiêu chuẩn"} | Bản vẽ:{" "}
              <span className="font-mono text-slate-800">{targetProduct.cadDrawing}</span>
            </div>
          </div>

          <div className="sm:text-right shrink-0">
            <span
              className={`inline-flex items-center gap-1.5 px-3 py-1 text-xs font-bold uppercase rounded-full border whitespace-nowrap ${
                isLockedForProduction
                  ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                  : targetProduct.bomStatus === "NOT_CREATED"
                    ? "bg-slate-100 text-slate-700 border-slate-200"
                    : "bg-amber-50 text-amber-800 border-amber-200"
              }`}
            >
              <span
                className={`w-1.5 h-1.5 rounded-full ${
                  isLockedForProduction
                    ? "bg-emerald-500"
                    : targetProduct.bomStatus === "NOT_CREATED"
                      ? "bg-slate-500"
                      : "bg-amber-500"
                }`}
              />
              {activeBomId ? `BOM #${activeBomId} • ` : ""}{bomStatusLabel(targetProduct.bomStatus)}
            </span>
            <div className="text-xs text-slate-500 mt-1.5">
              Lưu lần cuối: {targetProduct.lastSaved}
            </div>
          </div>
        </div>

        {/* Production Routing Card */}
        <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="text-base font-bold text-slate-900">
              Quy trình công đoạn sản xuất (Chuỗi trạm làm việc)
            </h2>
            <button
              type="button"
              onClick={() => setShowRoutingModal(true)}
              className="px-3.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg transition cursor-pointer"
            >
              Tùy chỉnh công đoạn &amp; Hold Point
            </button>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            {routingStages.map((stg, idx) => (
              <div key={`${stg.stageId}-${idx}`} className="flex items-center gap-2.5">
                {stg.isHoldPoint ? (
                  <div className="px-3.5 py-2 rounded-lg border-2 border-rose-500 bg-rose-50/40 text-sm font-bold text-rose-700 flex items-center gap-2 shadow-2xs">
                    <span className="px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-amber-100 text-amber-800 border border-amber-300">
                      HOLD POINT
                    </span>
                    <span>{stg.stageName}</span>
                  </div>
                ) : (
                  <div className="px-3.5 py-2 rounded-lg border border-slate-300 bg-white text-sm font-semibold text-slate-800">
                    {stg.stageName}
                  </div>
                )}

                {idx < routingStages.length - 1 && (
                  <span className="text-slate-400 text-sm font-bold">→</span>
                )}
              </div>
            ))}
          </div>
        </div>

        {/* Scrap Formula & Owner Loss Allowance Bar */}
        <div className="bg-white border border-slate-200 rounded-xl px-5 py-4 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="text-sm text-slate-700 flex flex-wrap items-center gap-2">
            <strong className="font-bold text-slate-900">Công thức tính tổng nhu cầu:</strong>
            <code className="font-mono text-xs text-blue-700 bg-blue-50 px-2.5 py-1 rounded border border-blue-200">
              Tổng nhu cầu = (Định mức 1 SP × Số lượng đặt {targetProduct.orderQty}) × (1 + % hao hụt)
            </code>
          </div>

          <div className="flex items-center gap-2.5 shrink-0">
            <label htmlFor="ownerScrapRate" className="text-sm font-semibold text-slate-700">
              Tỷ lệ hao hụt cho phép (3% - 15%):
            </label>
            <input
              id="ownerScrapRate"
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
        </div>

        {/* BOM Item Breakdown Table Card */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-wrap items-center justify-between gap-3 bg-white">
            <div className="flex items-center gap-2.5">
              <h2 className="text-base font-bold text-slate-900">
                Bảng bóc tách vật tư BOM
              </h2>
              {calculateDemandMutation.isPending && (
                <span className="text-xs text-blue-600 font-medium">
                  • Đang tính toán nhu cầu...
                </span>
              )}
            </div>

            <div className="flex items-center gap-2.5">
              <button
                type="button"
                onClick={() => setShowStagingModal(true)}
                className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg transition cursor-pointer"
              >
                Tải lên BOM Excel
              </button>

              <button
                type="button"
                onClick={() => {
                  setEditingMaterialItem(null);
                  setShowMaterialModal(true);
                }}
                className="px-3.5 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold rounded-lg shadow-2xs transition flex items-center gap-1.5 cursor-pointer"
              >
                <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                </svg>
                <span>Thêm vật tư</span>
              </button>
            </div>
          </div>

          <div className="overflow-x-auto bg-white">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-6 min-w-[140px]">Mã vật tư</th>
                  <th className="py-3.5 px-4 min-w-[120px]">Nhóm</th>
                  <th className="py-3.5 px-4 min-w-[260px]">Tên vật tư &amp; Quy cách chi tiết</th>
                  <th className="py-3.5 px-3 min-w-[80px]">ĐVT</th>
                  <th className="py-3.5 px-4 min-w-[120px]">Định mức / SP</th>
                  <th className="py-3.5 px-4 min-w-[150px]">Tổng định mức ({targetProduct.orderQty} cái)</th>
                  <th className="py-3.5 px-4 min-w-[140px]">Hao hụt</th>
                  <th className="py-3.5 px-4 min-w-[180px]">Tổng nhu cầu &amp; Tồn kho</th>
                  <th className="py-3.5 px-6 text-right min-w-[130px]">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm bg-white">
                {isLoadingBom ? (
                  <tr>
                    <td colSpan={9} className="py-12 text-center text-slate-500">
                      Đang tải bóc tách BOM từ máy chủ...
                    </td>
                  </tr>
                ) : bomItems.length === 0 ? (
                  <tr>
                    <td colSpan={9} className="py-12 text-center text-slate-500">
                      Dòng hàng <strong>#{selectedOrderItemId}</strong> chưa có nguyên vật liệu nào. Hãy sử dụng{" "}
                      <strong>Nhập BOM Excel</strong>, <strong>Kế thừa từ danh mục</strong> hoặc{" "}
                      <strong>Thêm vật tư</strong> để thiết lập định mức.
                    </td>
                  </tr>
                ) : (
                  bomItems.map((item) => {
                    const metrics = computeRowMetrics(item, targetProduct.orderQty, scrapRate);
                    return (
                      <tr
                        key={item.bomDetailId}
                        className="hover:bg-slate-50/90 transition-colors"
                      >
                        <td className="py-4 px-6 font-mono text-sm font-bold text-slate-800 whitespace-nowrap">
                          {item.materialCode}
                        </td>
                        <td className="py-4 px-4 whitespace-nowrap">
                          <span
                            className={`inline-block px-2.5 py-0.5 text-xs font-bold uppercase rounded border ${getCategoryBadgeClass(
                              item.materialGroup,
                            )}`}
                          >
                            {item.materialGroup}
                          </span>
                        </td>
                        <td className="py-4 px-4">
                          <div className="font-semibold text-slate-900">{item.materialName}</div>
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
                        <td className="py-4 px-3 text-slate-600 whitespace-nowrap">{item.unitOfMeasure}</td>
                        <td className="py-4 px-4 font-bold text-slate-900 whitespace-nowrap">
                          {item.netQtyPerUnit}
                        </td>
                        <td className="py-4 px-4 text-slate-700 whitespace-nowrap">{metrics.netSubtotalText}</td>
                        <td className="py-4 px-4 text-slate-600 whitespace-nowrap">
                          {metrics.scrapAllowanceText}
                        </td>
                        <td className="py-4 px-4 whitespace-nowrap">
                          <div className="font-extrabold text-blue-700">
                            {metrics.grossTotalText}
                          </div>
                          {item.availableStock !== undefined && (
                            <div className="mt-1 flex items-center gap-1.5 text-xs">
                              <span className="text-slate-500">
                                Khả dụng: <strong className="text-slate-800">{item.availableStock}</strong>
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
                        <td className="py-4 px-6 text-right whitespace-nowrap">
                          <div className="inline-flex items-center gap-2">
                            <button
                              type="button"
                              onClick={() => {
                                setEditingMaterialItem(item);
                                setShowMaterialModal(true);
                              }}
                              className="px-3 py-1.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg transition cursor-pointer"
                            >
                              Chỉnh sửa
                            </button>
                            <button
                              type="button"
                              onClick={() =>
                                handleDeleteMaterialItem(item.bomDetailId, item.materialCode)
                              }
                              className="px-2.5 py-1.5 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 text-xs font-bold rounded-lg transition cursor-pointer"
                              title="Xóa dòng vật tư"
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

      <VisualProductCatalogModal
        isOpen={showVisualCatalogModal}
        currentModelKey={targetProduct.modelKey}
        onClose={() => setShowVisualCatalogModal(false)}
        onSelectModel={handleSelectVisualModel}
      />

      <DynamicRoutingModal
        isOpen={showRoutingModal}
        stages={routingStages}
        onClose={() => setShowRoutingModal(false)}
        onSave={handleSaveRoutingSequence}
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
        orderItemId={selectedOrderItemId}
        productId={targetProduct.productId}
        productSku={targetProduct.sku}
        productName={targetProduct.productName}
        existingBomId={activeBomId}
        initialScrapRate={scrapRate}
        onClose={() => setShowStagingModal(false)}
        onConfirmedBom={handleConfirmedStagingBom}
      />
    </WmmsAppShell>
  );
}
