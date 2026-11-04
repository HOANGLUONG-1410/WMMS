import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
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

export default function BomBuilderPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { currentSpaceKey, setCurrentSpaceKey, setOrderSpaces } = useWmms();

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
    finishSpec: "Natural Walnut",
    fscRequired: true,
    cadDrawing: "CAD_Chair_880504.pdf",
    bomStatus: "DRAFT",
    lastSaved: "Not saved yet",
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
            spaceId: Number(o.spaceId || o.orderId || 1),
            spaceKey: String(o.orderSpaceKey || `SPACE-${o.poNumber}`),
            orderId: Number(o.orderId || 1),
            poNumber: String(o.poNumber || ""),
            customerName: String(o.customerName || ""),
            country: String(o.shippingCountry || "Export"),
            containerSpec: String(o.containerSpec || "1x40HC"),
            etdDate: o.etdDate ? String(o.etdDate).slice(0, 10) : "",
            daysLeft: Number(o.daysRemaining || 30),
            lifecycleStatus: String(o.orderStatus || "CONFIRMED"),
            progressPct: Number(o.bomReadinessPct || 0),
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
          serverBom.dimensionsCm || activeLineItem?.dimensionsSpec || "Standard Spec",
        finishSpec:
          activeLineItem?.finishSpec || activeLineItem?.woodSpecies || "Standard Finish",
        fscRequired: activeLineItem?.fscCertifiedReq ?? true,
        cadDrawing:
          activeLineItem?.attachedCadFileName || `CAD_${serverBom.productSku}.pdf`,
        bomStatus: serverBom.bomStatusText || String(serverBom.bomStatus || "DRAFT"),
        lastSaved: serverBom.updatedAt
          ? new Date(serverBom.updatedAt).toLocaleString("en-GB")
          : serverBom.createdAt
            ? new Date(serverBom.createdAt).toLocaleString("en-GB")
            : "Synced with Server",
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
        finishSpec: activeLineItem?.finishSpec || activeLineItem?.woodSpecies || "Standard Finish",
        fscRequired: activeLineItem?.fscCertifiedReq ?? true,
        cadDrawing: activeLineItem?.attachedCadFileName || `CAD_${prodSku}.pdf`,
        bomStatus: "NOT_CREATED",
        lastSaved: "Not created in DB yet",
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
          lastSaved: `Today ${nowTime}`,
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
      lastSaved: "Just imported from Excel Staging",
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

  // Màu sắc huy hiệu Category
  const getCategoryBadgeClass = (category: string) => {
    const upper = String(category || "").toUpperCase();
    if (upper.includes("LUMBER") || upper.includes("WOOD")) {
      return "bg-yellow-100 text-amber-800";
    }
    if (upper.includes("HARDWARE")) {
      return "bg-blue-100 text-blue-700";
    }
    if (upper.includes("FINISH") || upper.includes("PAINT")) {
      return "bg-orange-100 text-orange-800";
    }
    if (upper.includes("PACKAGING")) {
      return "bg-emerald-100 text-emerald-700";
    }
    return "bg-slate-100 text-slate-700";
  };

  return (
    <WmmsAppShell>
      <div className="max-w-[1400px] mx-auto space-y-4">
        {/* Thông báo phản hồi */}
        {toastBanner && (
          <div
            className={`px-4 py-3 rounded-lg border text-xs font-semibold flex items-center justify-between ${
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
              className="text-slate-400 hover:text-slate-600 ml-3 font-bold"
            >
              ✕
            </button>
          </div>
        )}

        {/* Header + Action Buttons */}
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
              BOM Specification, Excel Staging &amp; Dynamic Routing Builder
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Owner / Planner engineering breakdown: Import BOM Excel via Staging Grid, inherit
              visual catalog templates, configure routing &amp; lock BOM for production.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2 shrink-0">
            {/* Nút mở BOM Excel Staging Modal */}
            <button
              type="button"
              onClick={() => setShowStagingModal(true)}
              className="px-3.5 py-2 bg-blue-50 hover:bg-blue-100 text-blue-800 border border-blue-300 text-xs font-bold rounded-md shadow-2xs transition flex items-center gap-1.5"
            >
              <span>📥</span>
              <span>Import BOM Excel (Staging)</span>
            </button>

            {/* Field #1: Visual Product Catalog */}
            <button
              type="button"
              onClick={() => setShowVisualCatalogModal(true)}
              className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-800 border border-slate-300 text-xs font-semibold rounded-md shadow-2xs transition flex items-center gap-1.5"
            >
              <span>🖼️</span>
              <span>Visual Product Catalog</span>
            </button>

            {/* Field #2: Dynamic Routing Engine */}
            <button
              type="button"
              onClick={() => setShowRoutingModal(true)}
              className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-800 border border-slate-300 text-xs font-semibold rounded-md shadow-2xs transition flex items-center gap-1.5"
            >
              <span>🔀</span>
              <span>Dynamic Routing Engine</span>
            </button>

            {/* Field #3: Save Draft & Run Stock Reconciliation → */}
            <button
              type="button"
              onClick={handleSaveDraftAndReconcile}
              disabled={saveBomMutation.isPending}
              className="px-4 py-2 bg-[#d97706] hover:bg-amber-700 text-white text-xs font-bold rounded-md shadow-sm transition disabled:opacity-50"
            >
              {saveBomMutation.isPending
                ? "Saving to Server..."
                : activeBomId
                  ? "Update BOM Draft & Reconcile →"
                  : "Save New BOM Draft →"}
            </button>

            {/* Nút Phê duyệt & Chốt khóa BOM (PATCH /api/v1/bom/{bomId}/approve) */}
            {activeBomId && !isLockedForProduction && (
              <button
                type="button"
                onClick={handleApproveAndLockBom}
                disabled={approveBomMutation.isPending}
                className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold rounded-md shadow-sm transition disabled:opacity-50"
              >
                {approveBomMutation.isPending ? "Approving..." : "✓ Approve & Lock BOM"}
              </button>
            )}
          </div>
        </div>

        {/* Thanh chọn hạng mục sản phẩm (Order Line Items) thuộc Order Space hiện tại */}
        <div className="bg-white border border-slate-200 rounded-lg p-3.5 shadow-2xs space-y-2.5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2 text-xs">
              <span className="px-2 py-0.5 font-mono font-bold rounded bg-slate-100 text-slate-800 border border-slate-300">
                {currentSpaceKey || "ORDER SPACE"}
              </span>
              {spacePoNumber && (
                <span className="font-semibold text-slate-700">
                  PO: <strong>{spacePoNumber}</strong>
                </span>
              )}
              {spaceCustomerName && (
                <span className="text-slate-500">• Customer: {spaceCustomerName}</span>
              )}
            </div>
            <span className="text-[11px] text-slate-400">
              Select an Order Line Item below to create or edit its technical BOM:
            </span>
          </div>

          {isLoadingSpace ? (
            <div className="text-xs text-slate-400 py-1">
              Loading order line items from Order Space...
            </div>
          ) : spaceLineItems.length > 0 ? (
            <div className="flex flex-wrap items-center gap-2">
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
                    className={`px-3 py-2 rounded-lg border text-left transition flex items-center gap-2.5 ${
                      isSelected
                        ? "border-2 border-[#d97706] bg-amber-50/60 shadow-2xs"
                        : "border-slate-200 bg-slate-50/60 hover:bg-slate-100"
                    }`}
                  >
                    <div>
                      <div className="flex items-center gap-1.5">
                        <span className="font-mono text-[11px] font-extrabold text-slate-900">
                          #{li.orderItemId} • {li.productSku}
                        </span>
                        <span
                          className={`px-1.5 py-0.5 text-[9px] font-extrabold uppercase rounded ${
                            isReady
                              ? "bg-emerald-100 text-emerald-800"
                              : hasDraft
                                ? "bg-amber-100 text-amber-800"
                                : "bg-slate-200 text-slate-700"
                          }`}
                        >
                          {isReady ? "READY_PROD" : hasDraft ? `BOM #${li.bomId} DRAFT` : "NO BOM"}
                        </span>
                      </div>
                      <div className="text-[11px] text-slate-600 mt-0.5">
                        {li.productName} ({li.orderedQty} pcs)
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>
          ) : (
            <div className="text-xs text-slate-500">
              Active Order Item ID: <strong className="font-mono">#{selectedOrderItemId}</strong>{" "}
              (Switch Order Space on top bar to load other PO line items)
            </div>
          )}
        </div>

        {/* Khối Active BOM Target Product Banner */}
        <div className="bg-amber-50/50 border border-amber-300 rounded-lg p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <div className="text-[10px] font-extrabold uppercase tracking-wider text-amber-700">
              ACTIVE BOM TARGET PRODUCT (ORDER ITEM #{selectedOrderItemId} • PRODUCT ID #
              {targetProduct.productId}):
            </div>
            <div className="text-sm font-extrabold text-slate-900 mt-0.5">
              SKU: {targetProduct.sku} — {targetProduct.productName} (Order Qty:{" "}
              {targetProduct.orderQty} Units)
            </div>
            <div className="text-xs text-slate-600 mt-0.5">
              Dimensions: {targetProduct.dimensions} | Spec: {targetProduct.finishSpec} |{" "}
              {targetProduct.fscRequired ? "FSC 100% Lumber Required" : "Standard Lumber"} |
              Attached Drawing: {targetProduct.cadDrawing}
            </div>
          </div>

          <div className="sm:text-right shrink-0">
            <span
              className={`inline-block px-2.5 py-1 text-[10px] font-extrabold uppercase rounded border ${
                isLockedForProduction
                  ? "bg-emerald-100 text-emerald-800 border-emerald-300"
                  : targetProduct.bomStatus === "NOT_CREATED"
                    ? "bg-slate-100 text-slate-700 border-slate-300"
                    : "bg-yellow-100 text-amber-800 border-yellow-300"
              }`}
            >
              {activeBomId ? `BOM #${activeBomId} • ` : ""}STATUS: {targetProduct.bomStatus}
            </span>
            <div className="text-[11px] text-slate-400 mt-1">
              Last saved: {targetProduct.lastSaved}
            </div>
          </div>
        </div>

        {/* Khối Current Configured Production Routing (Sequence of Workstations) + Field #4 */}
        <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-2xs space-y-3.5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="text-xs font-bold text-slate-800">
              Current Configured Production Routing (Sequence of Workstations)
            </h2>
            {/* Field #4: Customize Steps & Hold Points */}
            <button
              type="button"
              onClick={() => setShowRoutingModal(true)}
              className="px-3 py-1 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-[11px] font-semibold rounded transition"
            >
              Customize Steps &amp; Hold Points
            </button>
          </div>

          <div className="flex flex-wrap items-center gap-2 pt-0.5">
            {routingStages.map((stg, idx) => (
              <div key={`${stg.stageId}-${idx}`} className="flex items-center gap-2">
                {stg.isHoldPoint ? (
                  <div className="px-3 py-2 rounded-md border-2 border-rose-500 bg-rose-50/20 text-xs font-bold text-rose-700 flex items-center gap-2 shadow-2xs">
                    <span className="px-1.5 py-0.5 text-[9px] font-extrabold uppercase rounded bg-yellow-100 text-amber-800 border border-yellow-300">
                      HOLD POINT
                    </span>
                    <span>{stg.stageName}</span>
                  </div>
                ) : (
                  <div className="px-3.5 py-2 rounded-md border border-slate-300 bg-white text-xs font-semibold text-slate-800">
                    {stg.stageName}
                  </div>
                )}

                {idx < routingStages.length - 1 && (
                  <span className="text-slate-400 text-xs font-bold">→</span>
                )}
              </div>
            ))}
          </div>
        </div>

        {/* Thanh công thức Auto-Scrap Formula + Field #5: Owner Loss Allowance % */}
        <div className="bg-slate-50 border border-slate-200 rounded-lg px-4 py-2.5 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="text-xs text-slate-700 flex flex-wrap items-center gap-2">
            <span>📐</span>
            <strong className="font-bold text-slate-900">Auto-Scrap Formula (BR-007):</strong>
            <code className="font-mono text-[11px] text-blue-700 bg-blue-50/80 px-2 py-0.5 rounded border border-blue-100">
              Total Gross Demand = (Net Quantity per Unit × Order Quantity {targetProduct.orderQty})
              × (1 + Scrap %)
            </code>
          </div>

          {/* Field #5: Owner Loss Allowance % */}
          <div className="flex items-center gap-2 shrink-0">
            <label htmlFor="ownerScrapRate" className="text-xs font-semibold text-slate-700">
              Owner Loss Allowance % (System Config 3% - 5%):
            </label>
            <input
              id="ownerScrapRate"
              type="number"
              step="0.1"
              min="3"
              max="15"
              value={scrapRate}
              onChange={(e) => handleScrapRateChange(e.target.value)}
              className="w-20 px-2.5 py-1 text-xs font-bold text-slate-900 bg-white border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-blue-500 text-center"
            />
            <span className="text-xs font-semibold text-slate-500">%</span>
          </div>
        </div>

        {/* Khối Bill of Materials (BOM) Item Breakdown + Fields #6, #7, #8 */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-2xs overflow-hidden">
          <div className="px-5 py-3.5 border-b border-slate-200 flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <h2 className="text-xs font-bold text-slate-800">
                Bill of Materials (BOM) Item Breakdown ({bomItems.length} items)
              </h2>
              {calculateDemandMutation.isPending && (
                <span className="text-[11px] text-blue-600 font-medium">
                  • Calculating server demand...
                </span>
              )}
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setShowStagingModal(true)}
                className="px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-blue-700 border border-blue-200 text-xs font-semibold rounded transition"
              >
                📥 Upload Excel BOM
              </button>

              {/* Field #6: + Add Material Item */}
              <button
                type="button"
                onClick={() => {
                  setEditingMaterialItem(null);
                  setShowMaterialModal(true);
                }}
                className="px-3 py-1.5 bg-white hover:bg-slate-50 text-slate-800 border border-slate-300 text-xs font-semibold rounded transition"
              >
                + Add Material Item
              </button>
            </div>
          </div>

          {/* Field #7: BOM Item Breakdown Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-5">Material Code</th>
                  <th className="py-3 px-4">Category</th>
                  <th className="py-3 px-4">Material Name, Sub-Assembly &amp; Dimensions</th>
                  <th className="py-3 px-3">UoM</th>
                  <th className="py-3 px-4">Net Qty / Unit</th>
                  <th className="py-3 px-4">Net Subtotal ({targetProduct.orderQty} Units)</th>
                  <th className="py-3 px-4">Scrap Allowance</th>
                  <th className="py-3 px-4">Gross Total Demand &amp; Stock</th>
                  <th className="py-3 px-5 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-xs">
                {isLoadingBom ? (
                  <tr>
                    <td colSpan={9} className="py-10 text-center text-slate-400">
                      Loading BOM breakdown from server...
                    </td>
                  </tr>
                ) : bomItems.length === 0 ? (
                  <tr>
                    <td colSpan={9} className="py-10 text-center text-slate-500">
                      Order Item <strong>#{selectedOrderItemId}</strong> chưa có dòng nguyên vật liệu
                      nào. Hãy nhấn <strong>&ldquo;📥 Import BOM Excel (Staging)&rdquo;</strong>,{" "}
                      <strong>&ldquo;🖼️ Visual Product Catalog&rdquo;</strong> hoặc{" "}
                      <strong>&ldquo;+ Add Material Item&rdquo;</strong> để thiết lập định mức.
                    </td>
                  </tr>
                ) : (
                  bomItems.map((item) => {
                    const metrics = computeRowMetrics(item, targetProduct.orderQty, scrapRate);
                    return (
                      <tr
                        key={item.bomDetailId}
                        className="hover:bg-slate-50/80 transition-colors"
                      >
                        <td className="py-3.5 px-5 font-mono text-[11px] font-bold text-slate-800">
                          {item.materialCode}
                        </td>
                        <td className="py-3.5 px-4">
                          <span
                            className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded ${getCategoryBadgeClass(
                              item.materialGroup,
                            )}`}
                          >
                            {item.materialGroup}
                          </span>
                        </td>
                        <td className="py-3.5 px-4">
                          <div className="font-semibold text-slate-900">{item.materialName}</div>
                          {(item.subAssemblyName || item.componentDimensionSpec) && (
                            <div className="flex flex-wrap items-center gap-1.5 mt-1">
                              {item.subAssemblyName && (
                                <span className="px-1.5 py-0.5 text-[10px] font-bold rounded bg-amber-100 text-amber-900 border border-amber-200">
                                  {item.subAssemblyName}
                                </span>
                              )}
                              {item.componentDimensionSpec && (
                                <span className="font-mono text-[11px] font-semibold text-blue-700 bg-blue-50 px-1.5 py-0.5 rounded border border-blue-200">
                                  {item.componentDimensionSpec}
                                </span>
                              )}
                            </div>
                          )}
                          {(item.dimensionSpec || item.notes) && (
                            <div className="text-[11px] text-slate-500 mt-0.5">
                              {item.dimensionSpec}
                              {item.dimensionSpec && item.notes ? " • " : ""}
                              {item.notes}
                            </div>
                          )}
                        </td>
                        <td className="py-3.5 px-3 text-slate-600">{item.unitOfMeasure}</td>
                        <td className="py-3.5 px-4 font-semibold text-slate-800">
                          {item.netQtyPerUnit}
                        </td>
                        <td className="py-3.5 px-4 text-slate-700">{metrics.netSubtotalText}</td>
                        <td className="py-3.5 px-4 text-slate-600">
                          {metrics.scrapAllowanceText}
                        </td>
                        <td className="py-3.5 px-4">
                          <div className="font-extrabold text-[#d97706]">
                            {metrics.grossTotalText}
                          </div>
                          {item.availableStock !== undefined && (
                            <div className="mt-0.5 flex items-center gap-1.5 text-[10px]">
                              <span className="text-slate-500">
                                Avail: <strong>{item.availableStock}</strong>
                              </span>
                              {item.isShortage ? (
                                <span className="px-1.5 py-0.2 rounded bg-rose-100 text-rose-700 font-bold">
                                  Thiếu: {item.netDemand}
                                </span>
                              ) : (
                                <span className="px-1.5 py-0.2 rounded bg-emerald-100 text-emerald-700 font-bold">
                                  Đủ tồn
                                </span>
                              )}
                            </div>
                          )}
                        </td>
                        <td className="py-3.5 px-5 text-right">
                          <div className="inline-flex items-center gap-1.5">
                            {/* Field #8: Edit Button */}
                            <button
                              type="button"
                              onClick={() => {
                                setEditingMaterialItem(item);
                                setShowMaterialModal(true);
                              }}
                              className="px-2.5 py-1 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-[11px] font-semibold rounded transition"
                            >
                              Edit
                            </button>
                            <button
                              type="button"
                              onClick={() =>
                                handleDeleteMaterialItem(item.bomDetailId, item.materialCode)
                              }
                              className="px-2 py-1 bg-white hover:bg-rose-50 text-rose-600 border border-slate-200 text-[11px] font-bold rounded transition"
                              title="Remove material line"
                            >
                              ✕
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

      {/* Modal 5.2: Visual Product Catalog Selection */}
      <VisualProductCatalogModal
        isOpen={showVisualCatalogModal}
        currentModelKey={targetProduct.modelKey}
        onClose={() => setShowVisualCatalogModal(false)}
        onSelectModel={handleSelectVisualModel}
      />

      {/* Modal 5.3: Customize Dynamic Routing Sequence */}
      <DynamicRoutingModal
        isOpen={showRoutingModal}
        stages={routingStages}
        onClose={() => setShowRoutingModal(false)}
        onSave={handleSaveRoutingSequence}
      />

      {/* Modal Add / Edit Material Item */}
      <MaterialItemModal
        isOpen={showMaterialModal}
        editingItem={editingMaterialItem}
        onClose={() => {
          setShowMaterialModal(false);
          setEditingMaterialItem(null);
        }}
        onSave={handleSaveMaterialItem}
      />

      {/* Modal BOM Excel Staging Preview & Confirm */}
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
