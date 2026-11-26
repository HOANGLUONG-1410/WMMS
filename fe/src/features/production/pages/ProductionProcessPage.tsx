// fe/src/features/production/pages/ProductionProcessPage.tsx
import { useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { ApiError } from "../../../lib/api/apiError";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useWmms } from "../../auth/hooks/WmmsContext";
import { useSession } from "../../auth/store/SessionContext";
import { orderIntakeApi } from "../../../services/wmmsApi";
import { bomApi } from "../../bom/api/bomApi";
import { mrpApi } from "../../inventory/api/mrpApi";
import { productionApi } from "../api/productionApi";
import type { BomDto, OrderSpaceLineItemForBom, RoutingStageDto } from "../../bom/types";
import type { MaterialOrderAllocationDto, MrpAnalysisResultDto } from "../../inventory/types";
import type {
  AdvanceStepRequestDto,
  ApproveOverdriveDto,
  BatchRoutingStepDto,
  CompleteFinishedBatchRequestDto,
  OrderProductionProgressDto,
  ProductionBatchDto,
  RecordQcInspectionDto,
  ReinspectSubBatchDto,
  RequestOverdriveDto,
} from "../types";

const fmtNum = (n?: number | null) => (n ?? 0).toLocaleString("vi-VN");

const fmtDate = (v?: string | null) => {
  if (!v) return "—";
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleDateString("vi-VN");
};

const fmtDateTime = (v?: string | null) => {
  if (!v) return "—";
  const d = new Date(v);
  if (Number.isNaN(d.getTime())) return v;
  return d.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
};

/**
 * Suy diễn thời gian bắt đầu và kết thúc thực tế của từng công đoạn dựa trên chuỗi tuần tự S -> S + 1.
 * - Công đoạn 1: bắt đầu tại `batch.createdAt` nếu lô đã kích hoạt chạy (không bị LOCKED ngay từ đầu).
 * - Công đoạn S (S > 1): bắt đầu tại `completedAt` của công đoạn S - 1 liền trước nếu công đoạn S đã rời trạng thái WAITING hoặc đang là công đoạn hiện tại.
 * - Kết thúc: lấy trực tiếp `step.completedAt`.
 */
const getStepTimestamps = (
  steps: BatchRoutingStepDto[],
  index: number,
  batch: ProductionBatchDto,
): { startedAt: string | null; completedAt: string | null } => {
  const step = steps[index];
  if (!step) return { startedAt: null, completedAt: null };

  const completedAt = step.completedAt || null;
  const hasNotStartedYet =
    batch.batchStatus === "LOCKED" &&
    step.stepStatus === "WAITING" &&
    step.passedQty === 0 &&
    !completedAt;

  if (hasNotStartedYet) {
    return { startedAt: null, completedAt: null };
  }

  const isStarted =
    step.stepStatus !== "WAITING" ||
    step.passedQty > 0 ||
    step.defectQty > 0 ||
    Boolean(completedAt) ||
    (batch.batchStatus !== "LOCKED" && step.stageSequence <= batch.currentStageSeq);

  if (!isStarted) {
    return { startedAt: null, completedAt };
  }

  if (index === 0) {
    return { startedAt: batch.createdAt || null, completedAt };
  }

  const prevStep = steps[index - 1];
  return {
    startedAt: prevStep?.completedAt || null,
    completedAt,
  };
};

/**
 * Ánh xạ nhãn Tiếng Việt và màu sắc cho trạng thái Thẻ lô (BatchStatusEnum).
 */
const getBatchStatusMeta = (status?: string) => {
  switch (status) {
    case "LOCKED":
      return {
        label: "Tạm khóa (Chờ xuất kho)",
        badgeClass: "bg-amber-100 text-amber-800 border border-amber-300",
      };
    case "IN_PROGRESS":
      return {
        label: "Đang sản xuất",
        badgeClass: "bg-blue-100 text-blue-800 border border-blue-200",
      };
    case "QC_PENDING":
      return {
        label: "Chờ kiểm định KCS",
        badgeClass: "bg-indigo-100 text-indigo-800 border border-indigo-200",
      };
    case "HOLD_POINT_BLOCKED":
      return {
        label: "Khóa cứng Hold Point",
        badgeClass: "bg-rose-100 text-rose-800 border border-rose-300",
      };
    case "DONE":
      return {
        label: "Đã hoàn tất",
        badgeClass: "bg-emerald-100 text-emerald-800 border border-emerald-200",
      };
    default:
      return {
        label: status || "Không xác định",
        badgeClass: "bg-slate-100 text-slate-700 border border-slate-200",
      };
  }
};

/**
 * Ánh xạ nhãn Tiếng Việt và màu sắc cho trạng thái Công đoạn (StepStatusEnum).
 */
const getStepStatusMeta = (
  step: BatchRoutingStepDto,
  isActiveStage: boolean,
  batchStatus: string,
) => {
  if (step.stepStatus === "PASSED") {
    return {
      label: "ĐÃ HOÀN TẤT",
      badgeClass: "bg-emerald-100 text-emerald-800 border border-emerald-200",
    };
  }
  if (step.stepStatus === "BLOCKED" || (isActiveStage && batchStatus === "HOLD_POINT_BLOCKED")) {
    return {
      label: "BỊ CHẶN HOLD POINT",
      badgeClass: "bg-rose-100 text-rose-800 border border-rose-300",
    };
  }
  if (isActiveStage && batchStatus === "LOCKED") {
    return {
      label: "KHÓA CHỜ VẬT TƯ",
      badgeClass: "bg-amber-100 text-amber-800 border border-amber-300",
    };
  }
  if (step.stepStatus === "QC_PENDING") {
    return {
      label: "CHỜ KCS",
      badgeClass: "bg-indigo-100 text-indigo-800 border border-indigo-200",
    };
  }
  if (step.stepStatus === "IN_PROGRESS" || isActiveStage) {
    return {
      label: "ĐANG THỰC HIỆN",
      badgeClass: "bg-blue-100 text-blue-800 border border-blue-200",
    };
  }
  return {
    label: "CHƯA BẮT ĐẦU",
    badgeClass: "bg-slate-100 text-slate-600 border border-slate-200",
  };
};

interface SpaceLineItemView {
  orderItemId: number;
  productId: number;
  productSku: string;
  productName: string;
  orderedQty: number;
  bomId?: number | null;
  itemStatus?: string;
  bomLifecycleBadge?: string;
}

export default function ProductionProcessPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { currentSpaceKey, setCurrentSpaceKey } = useWmms();
  const { currentUser } = useSession();

  const spaceParam = searchParams.get("spaceKey");
  const activeSpaceKey = spaceParam || currentSpaceKey || "";

  // Quyền thực tế của người dùng hiện tại
  const activeRole = currentUser?.activeRole || "OWNER";
  const isOwnerOrAdmin = activeRole === "OWNER" || activeRole === "ADMIN";
  const isQcAuthorized = isOwnerOrAdmin || activeRole === "QC";
  const isLeaderAuthorized = isOwnerOrAdmin || activeRole === "LEADER";
  const isWarehouseAuthorized = isOwnerOrAdmin || activeRole === "WAREHOUSE";
  const canQueryBom = ["ADMIN", "OWNER", "PLANNER", "LEADER", "PURCHASING", "WAREHOUSE"].includes(
    activeRole,
  );
  const canQueryMrp = ["ADMIN", "OWNER", "PLANNER", "PURCHASING", "WAREHOUSE"].includes(activeRole);
  const canAccessBomPage = ["ADMIN", "OWNER", "PLANNER"].includes(activeRole);
  const canAccessReconciliationPage = [
    "ADMIN",
    "OWNER",
    "PLANNER",
    "WAREHOUSE",
    "PURCHASING",
  ].includes(activeRole);

  // Thông tin Không gian đơn hàng
  const [spaceInfo, setSpaceInfo] = useState<{
    spaceId?: number;
    orderId?: number;
    spaceKey: string;
    poNumber: string;
    customerName: string;
    etdDate?: string;
    bomStatusTitle?: string;
    materialAllocationTitle?: string;
    lineItems: SpaceLineItemView[];
  } | null>(null);

  const [isLoadingSpace, setIsLoadingSpace] = useState<boolean>(true);
  const [spaceError, setSpaceError] = useState<string | null>(null);

  // Dòng sản phẩm đang chọn trong Space
  const [selectedOrderItemId, setSelectedOrderItemId] = useState<number | null>(null);

  // Danh sách Lô sản xuất của Space
  const [batches, setBatches] = useState<ProductionBatchDto[]>([]);
  const [isLoadingBatches, setIsLoadingBatches] = useState<boolean>(false);
  const [selectedBatchId, setSelectedBatchId] = useState<number | null>(null);

  // Điều kiện tiên quyết BOM, Quy trình mẫu & MRP
  const [productRoutingStages, setProductRoutingStages] = useState<RoutingStageDto[]>([]);
  const [itemBom, setItemBom] = useState<BomDto | null>(null);
  const [bomCheckState, setBomCheckState] = useState<"LOADING" | "FOUND" | "NOT_CREATED" | "RESTRICTED">(
    "LOADING",
  );
  const [mrpAnalysis, setMrpAnalysis] = useState<MrpAnalysisResultDto | null>(null);
  const [spaceAllocations, setSpaceAllocations] = useState<MaterialOrderAllocationDto[]>([]);
  const [orderProgress, setOrderProgress] = useState<OrderProductionProgressDto | null>(null);

  // Banner thông báo kết quả
  const [toastBanner, setToastBanner] = useState<{
    type: "success" | "error" | "info";
    text: string;
  } | null>(null);

  // Action States
  const [isGeneratingBatches, setIsGeneratingBatches] = useState<boolean>(false);
  const [isSubmittingAction, setIsSubmittingAction] = useState<boolean>(false);

  // Modal 1: Thiết lập Phát hành Lệnh sản xuất & Tùy chỉnh BatchSize
  const [isGenerateModalOpen, setIsGenerateModalOpen] = useState<boolean>(false);
  const [splitMode, setSplitMode] = useState<"MAX_SIZE" | "CUSTOM">("MAX_SIZE");
  const [maxBatchSizeInput, setMaxBatchSizeInput] = useState<number>(100);
  const [customSplitsInput, setCustomSplitsInput] = useState<number[]>([100]);
  const [allowOverdriveInput, setAllowOverdriveInput] = useState<boolean>(false);
  const [overdriveJustificationInput, setOverdriveJustificationInput] = useState<string>("");

  // Modal 2: Nghiệm thu KCS Lô chính (QC Inspect)
  const [qcModalStep, setQcModalStep] = useState<BatchRoutingStepDto | null>(null);
  const [qcForm, setQcForm] = useState<RecordQcInspectionDto>({
    batchStepId: 0,
    inspectedQty: 0,
    passQty: 0,
    failQty: 0,
    defectCode: "",
    defectName: "",
    disposition: "REWORK_SUB_CARD",
    notes: "",
  });

  // Modal 3: Nghiệm thu lại Lô phụ (Re-inspect Sub-batch)
  const [reinspectModalStep, setReinspectModalStep] = useState<BatchRoutingStepDto | null>(null);
  const [reinspectForm, setReinspectForm] = useState<ReinspectSubBatchDto>({
    batchStepId: 0,
    inspectedQty: 0,
    passQty: 0,
    failQty: 0,
    notes: "",
  });

  // Modal 4: Chuyển công đoạn (Advance Step)
  const [advanceModalStep, setAdvanceModalStep] = useState<BatchRoutingStepDto | null>(null);
  const [advanceForm, setAdvanceForm] = useState<AdvanceStepRequestDto>({
    targetStageSeq: null,
    notes: "",
  });

  // Modal 5: Gộp lô phụ vào lô chính (Merge Sub-batch)
  const [mergeModalBatch, setMergeModalBatch] = useState<ProductionBatchDto | null>(null);
  const [mergeReasonInput, setMergeReasonInput] = useState<string>("");

  // Modal 6: Chuyển bước độc lập Lô phụ (Continue Independently)
  const [continueSubModalStep, setContinueSubModalStep] = useState<BatchRoutingStepDto | null>(null);
  const [continueSubNotesInput, setContinueSubNotesInput] = useState<string>("");

  // Modal 7: Gửi yêu cầu Overdrive (Tổ trưởng)
  const [requestOverdriveBatch, setRequestOverdriveBatch] = useState<ProductionBatchDto | null>(null);
  const [requestOverdriveForm, setRequestOverdriveForm] = useState<RequestOverdriveDto>({
    holdPointStageSeq: 5,
    justification: "",
  });

  // Modal 8: Phê duyệt / Từ chối Overdrive (Chủ xưởng / Admin)
  const [approveOverdriveBatch, setApproveOverdriveBatch] = useState<ProductionBatchDto | null>(null);
  const [approveOverdriveForm, setApproveOverdriveForm] = useState<ApproveOverdriveDto>({
    approved: true,
    holdPointStageSeq: 5,
    notes: "",
  });

  // Modal 9: Nhập kho thành phẩm hoàn tất lô
  const [completeBatchModal, setCompleteBatchModal] = useState<ProductionBatchDto | null>(null);
  const [completeForm, setCompleteForm] = useState<CompleteFinishedBatchRequestDto>({
    receivedQty: null,
    warehouseLocation: "KHO-TP-A1",
    handoverNote: "",
  });

  // Modal 10: Chi tiết công đoạn (kèm Ngày bắt đầu / Ngày kết thúc)
  const [detailModalContext, setDetailModalContext] = useState<{
    step: BatchRoutingStepDto;
    startedAt: string | null;
    completedAt: string | null;
  } | null>(null);

  useEffect(() => {
    if (spaceParam && spaceParam !== currentSpaceKey) {
      setCurrentSpaceKey(spaceParam);
    }
  }, [spaceParam, currentSpaceKey, setCurrentSpaceKey]);

  // Tải thông tin Không gian đơn hàng & dữ liệu phụ trợ
  const loadOrderSpaceData = async (spaceKey: string) => {
    if (!spaceKey) {
      setIsLoadingSpace(false);
      setSpaceError(
        "Không tìm thấy mã Không gian đơn hàng. Vui lòng chọn Không gian từ Danh sách đơn hàng.",
      );
      return;
    }

    setIsLoadingSpace(true);
    setSpaceError(null);
    try {
      const res = await orderIntakeApi.getOrderSpaceDetail(spaceKey);
      const detail = res?.data;
      if (detail) {
        const orderId = Number(detail.orderId || detail.id || 0);
        const spaceId = detail.spaceId ? Number(detail.spaceId) : undefined;
        const rawItems: OrderSpaceLineItemForBom[] = Array.isArray(detail.lineItems)
          ? detail.lineItems
          : [];

        const items: SpaceLineItemView[] = rawItems.map((li) => ({
          orderItemId: Number(li.orderItemId),
          productId: Number(li.productId),
          productSku: li.productSku || "",
          productName: li.productName || "",
          orderedQty: Number(li.orderedQty || 0),
          bomId: li.bomId,
          itemStatus: li.itemStatus,
          bomLifecycleBadge: li.bomLifecycleBadge,
        }));

        setSpaceInfo({
          spaceId,
          orderId,
          spaceKey: detail.spaceKey || spaceKey,
          poNumber: detail.poNumber || "",
          customerName: detail.customerName || "",
          etdDate: detail.etdDate,
          bomStatusTitle: detail.bomStatusTitle,
          materialAllocationTitle: detail.materialAllocationTitle,
          lineItems: items,
        });

        if (items.length > 0) {
          const targetParamId = searchParams.get("orderItemId")
            ? Number(searchParams.get("orderItemId"))
            : null;
          const matchedTarget = targetParamId && items.some((i) => i.orderItemId === targetParamId);
          setSelectedOrderItemId((prev) =>
            matchedTarget
              ? targetParamId
              : prev && items.some((i) => i.orderItemId === prev)
              ? prev
              : items[0].orderItemId,
          );
        }

        // Tải song song Lô sản xuất & Tiến độ tổng đơn
        await loadBatches(detail.spaceKey || spaceKey);

        if (orderId > 0) {
          productionApi
            .getOrderProgressById(orderId)
            .then((prog) => setOrderProgress(prog))
            .catch(() => setOrderProgress(null));
        }

        // Nếu vai trò có quyền MRP, tải trạng thái vật tư
        if (canQueryMrp) {
          if (spaceId) {
            mrpApi
              .analyzeOrderSpaceMrp(spaceId)
              .then((m) => setMrpAnalysis(m))
              .catch(() => setMrpAnalysis(null));
            mrpApi
              .getOrderSpaceAllocations(spaceId)
              .then((allocs) => setSpaceAllocations(Array.isArray(allocs) ? allocs : []))
              .catch(() => setSpaceAllocations([]));
          } else if (orderId > 0) {
            mrpApi
              .analyzeOrderMrp(orderId)
              .then((m) => setMrpAnalysis(m))
              .catch(() => setMrpAnalysis(null));
          }
        }
      } else {
        setSpaceError(`Lỗi: Không tìm thấy dữ liệu cho Không gian "${spaceKey}".`);
      }
    } catch (err: any) {
      setSpaceError(
        err?.response?.data?.message ||
        err?.message ||
        `Không thể tải dữ liệu Không gian đơn hàng "${spaceKey}" từ máy chủ.`
      );
    } finally {
      setIsLoadingSpace(false);
    }
  };

  // Tải danh sách lô sản xuất của Space
  const loadBatches = async (spaceKey: string) => {
    setIsLoadingBatches(true);
    try {
      const batchList = await productionApi.getBatchesBySpace(spaceKey);
      const safeList = Array.isArray(batchList) ? batchList : [];
      setBatches(safeList);
      if (safeList.length > 0) {
        setSelectedBatchId((prev) => {
          if (prev && safeList.some((b) => b.batchId === prev)) return prev;
          return safeList[0].batchId;
        });
      } else {
        setSelectedBatchId(null);
      }
    } catch {
      setBatches([]);
    } finally {
      setIsLoadingBatches(false);
    }
  };

  useEffect(() => {
    loadOrderSpaceData(activeSpaceKey);
  }, [activeSpaceKey]);

  // Sản phẩm hiện tại đang chọn
  const currentLineItem = useMemo(() => {
    if (!spaceInfo || spaceInfo.lineItems.length === 0) return null;
    return (
      spaceInfo.lineItems.find((li) => li.orderItemId === selectedOrderItemId) ||
      spaceInfo.lineItems[0]
    );
  }, [spaceInfo, selectedOrderItemId]);

  // Kiểm tra trạng thái BOM & Quy trình mẫu khi đổi dòng sản phẩm
  useEffect(() => {
    if (!currentLineItem) return;

    if (canQueryBom) {
      setBomCheckState("LOADING");
      bomApi
        .getBomByOrderItemId(currentLineItem.orderItemId)
        .then((res) => {
          if (res?.data) {
            setItemBom(res.data);
            setBomCheckState("FOUND");
            if (res.data.routingStages && res.data.routingStages.length > 0) {
              setProductRoutingStages(res.data.routingStages);
              return;
            }
          } else {
            setItemBom(null);
            setBomCheckState("NOT_CREATED");
          }
        })
        .catch((err) => {
          setItemBom(null);
          if (isAxiosError(err) && err.response?.status === 403) {
            setBomCheckState("RESTRICTED");
          } else {
            setBomCheckState("NOT_CREATED");
          }
        });

      bomApi
        .getProductRouting(currentLineItem.productId)
        .then((res) => {
          if (res?.data && res.data.length > 0) {
            setProductRoutingStages(res.data);
          } else {
            return bomApi.getDefaultRouting(currentLineItem.productId).then((defRes) => {
              setProductRoutingStages(defRes?.data || []);
            });
          }
        })
        .catch(() => {
          setProductRoutingStages([]);
        });
    } else {
      setBomCheckState("RESTRICTED");
      setItemBom(null);
    }
  }, [currentLineItem?.orderItemId, currentLineItem?.productId, canQueryBom]);

  // Danh sách lô thuộc sản phẩm đang chọn
  const currentItemBatches = useMemo(() => {
    if (!currentLineItem) return [];
    return batches.filter((b) => b.orderItemId === currentLineItem.orderItemId);
  }, [batches, currentLineItem]);

  // Tổng hợp sản lượng đã phát hành & còn lại chưa phát hành
  const releaseSummary = useMemo(() => {
    if (!currentLineItem) {
      return {
        orderedQty: 0,
        mainBatchesCount: 0,
        subBatchesCount: 0,
        initialMainReleasedQty: 0,
        backendMainQtySum: 0,
        remainingUnreleasedQty: 0,
      };
    }

    const mainBatches = currentItemBatches.filter((b) => b.batchType === "MAIN_CARD");
    const subBatches = currentItemBatches.filter((b) => b.batchType !== "MAIN_CARD");

    const initialMainReleasedQty = mainBatches.reduce((sum, b) => {
      const firstTarget = b.steps?.[0]?.targetQty;
      const originalQty = firstTarget && firstTarget > b.batchQty ? firstTarget : b.batchQty;
      return sum + originalQty;
    }, 0);

    const backendMainQtySum = mainBatches.reduce((sum, b) => sum + b.batchQty, 0);
    const remainingUnreleasedQty = Math.max(0, currentLineItem.orderedQty - initialMainReleasedQty);

    return {
      orderedQty: currentLineItem.orderedQty,
      mainBatchesCount: mainBatches.length,
      subBatchesCount: subBatches.length,
      initialMainReleasedQty,
      backendMainQtySum,
      remainingUnreleasedQty,
    };
  }, [currentItemBatches, currentLineItem]);

  // Trạng thái điều kiện tiên quyết BOM & Vật tư của sản phẩm hiện tại
  const prerequisiteState = useMemo(() => {
    const bomStatusRaw = String(
      itemBom?.bomStatusText ||
        itemBom?.bomStatus ||
        currentLineItem?.bomLifecycleBadge ||
        "",
    ).toUpperCase();

    const hasBomRecord =
      bomCheckState === "FOUND" ||
      Boolean(currentLineItem?.bomId) ||
      (bomCheckState === "RESTRICTED" &&
        bomStatusRaw !== "" &&
        bomStatusRaw !== "NOT_CREATED" &&
        bomStatusRaw !== "PENDING_BOM");

    const isBomApproved =
      bomStatusRaw === "READY_FOR_PRODUCTION" ||
      bomStatusRaw === "APPROVED" ||
      bomStatusRaw === "3" ||
      bomStatusRaw === "5";

    const itemAllocations = currentLineItem
      ? spaceAllocations.filter((a) => a.orderItemId === currentLineItem.orderItemId)
      : [];

    const isMaterialIssuedToShop =
      itemAllocations.length > 0 &&
      itemAllocations.every(
        (a) => a.allocationStatus === "ISSUED_TO_SHOP" || a.allocationStatus === "RELEASED",
      );

    const hasReservedAllocations =
      itemAllocations.length > 0 &&
      itemAllocations.some((a) => (a.reservedQty || 0) > 0 || a.allocationStatus === "RESERVED");

    return {
      hasBomRecord,
      isBomApproved,
      bomStatusLabel: itemBom?.bomStatusText || currentLineItem?.bomLifecycleBadge || (hasBomRecord ? "Đã thiết lập" : "Chưa lập BOM"),
      isMaterialIssuedToShop,
      hasReservedAllocations,
      shortageMaterialsCount: mrpAnalysis?.shortageMaterialsCount ?? 0,
    };
  }, [itemBom, bomCheckState, currentLineItem, spaceAllocations, mrpAnalysis]);

  // Lô đang được chọn để hiển thị tiến trình
  const activeBatch = useMemo(() => {
    if (currentItemBatches.length === 0) return null;
    return currentItemBatches.find((b) => b.batchId === selectedBatchId) || currentItemBatches[0];
  }, [currentItemBatches, selectedBatchId]);

  const isSubBatch = Boolean(
    activeBatch &&
      (activeBatch.batchType === "REWORK_SUB_CARD" || activeBatch.batchType === "REPLENISH_LOT"),
  );

  // Chỉ lô phụ sửa chữa (REWORK_SUB_CARD) mới gọi endpoint reinspect; lô bù phế phẩm (REPLENISH_LOT) đi từ trạm đầu như lô chính
  const isReworkSubBatch = Boolean(
    activeBatch && activeBatch.batchType === "REWORK_SUB_CARD",
  );

  const parentMainBatch = useMemo(() => {
    if (!activeBatch || !activeBatch.parentBatchId) return null;
    return batches.find((b) => b.batchId === activeBatch.parentBatchId) || null;
  }, [activeBatch, batches]);

  // Công đoạn hiện tại của lô đang chọn (dựa vào currentStageSeq)
  const activeStep = useMemo(() => {
    if (!activeBatch || !activeBatch.steps || activeBatch.steps.length === 0) return null;
    const matched = activeBatch.steps.find((s) => s.stageSequence === activeBatch.currentStageSeq);
    return matched || activeBatch.steps[0];
  }, [activeBatch]);

  const activeStepIndex = useMemo(() => {
    if (!activeBatch || !activeStep) return -1;
    return activeBatch.steps.findIndex((s) => s.batchStepId === activeStep.batchStepId);
  }, [activeBatch, activeStep]);

  const isBatchDone = activeBatch?.batchStatus === "DONE";
  const isBatchLocked = activeBatch?.batchStatus === "LOCKED";
  const isBatchHoldBlocked = activeBatch?.batchStatus === "HOLD_POINT_BLOCKED";

  // Công đoạn hiện tại đã đạt 100% KCS hay chưa (khớp Backend: StepStatus == PASSED hoặc PassedQty >= BatchQty)
  const isCurrentStepPassed = useMemo(() => {
    if (!activeBatch || !activeStep) return false;
    return (
      activeStep.stepStatus === "PASSED" ||
      (activeStep.passedQty >= activeBatch.batchQty && activeBatch.batchQty > 0)
    );
  }, [activeBatch, activeStep]);

  const isFinalStep = useMemo(() => {
    if (!activeBatch || !activeStep) return false;
    const maxSeq = Math.max(...activeBatch.steps.map((s) => s.stageSequence));
    return activeStep.stageSequence === maxSeq;
  }, [activeBatch, activeStep]);

  // Có thể gộp lô phụ vào lô chính không?
  const canMergeWithParent = useMemo(() => {
    if (!isSubBatch || !activeBatch || !parentMainBatch) return false;
    return (
      !isBatchDone &&
      parentMainBatch.batchStatus !== "DONE" &&
      parentMainBatch.currentStageSeq === activeBatch.currentStageSeq &&
      isCurrentStepPassed
    );
  }, [isSubBatch, activeBatch, parentMainBatch, isBatchDone, isCurrentStepPassed]);

  // Tính toán danh sách lô dự kiến (Live Preview) trong Modal Phát hành Lệnh sản xuất
  const previewBatchSplits = useMemo(() => {
    const remaining = releaseSummary.remainingUnreleasedQty;
    if (remaining <= 0) return [];

    if (splitMode === "CUSTOM") {
      return customSplitsInput.filter((q) => Number.isFinite(q) && q > 0);
    }

    const cap = Math.max(1, Math.min(10000, Number(maxBatchSizeInput) || remaining));
    const list: number[] = [];
    let temp = remaining;
    while (temp > 0) {
      const sz = Math.min(temp, cap);
      list.push(sz);
      temp -= sz;
    }
    return list;
  }, [splitMode, maxBatchSizeInput, customSplitsInput, releaseSummary.remainingUnreleasedQty]);

  const previewSplitsTotal = useMemo(
    () => previewBatchSplits.reduce((s, q) => s + q, 0),
    [previewBatchSplits],
  );

  // Mở Modal Thiết lập Phát hành Lệnh sản xuất
  const handleOpenGenerateModal = () => {
    const rem = releaseSummary.remainingUnreleasedQty;
    setSplitMode("MAX_SIZE");
    setMaxBatchSizeInput(rem > 0 ? rem : 100);
    setCustomSplitsInput(rem > 0 ? [rem] : [100]);
    // Nếu chưa xuất kho vật tư ra xưởng hoặc BOM chưa Approved, gợi ý rõ cho người dùng biết
    const shouldSuggestOverdrive =
      !prerequisiteState.isMaterialIssuedToShop || !prerequisiteState.isBomApproved;
    setAllowOverdriveInput(false);
    setOverdriveJustificationInput(
      shouldSuggestOverdrive
        ? "Kích hoạt chạy sơ chế phôi trước điểm dừng kỹ thuật (Hold Point) để đảm bảo tiến độ giao hàng ETD."
        : "",
    );
    setIsGenerateModalOpen(true);
  };

  // Thao tác 1: Submit Phát hành Lệnh sản xuất
  const handleSubmitGenerateBatches = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentLineItem) return;

    if (!prerequisiteState.hasBomRecord) {
      setToastBanner({
        type: "error",
        text: "Sản phẩm chưa có định mức BOM. Vui lòng thiết lập BOM trước khi phát hành lệnh sản xuất.",
      });
      return;
    }

    if (!prerequisiteState.isBomApproved && !allowOverdriveInput) {
      setToastBanner({
        type: "error",
        text: "BOM của sản phẩm chưa được phê duyệt (READY_FOR_PRODUCTION). Vui lòng duyệt BOM trước hoặc bật chế độ chạy trước Overdrive kèm giải trình.",
      });
      return;
    }

    if (previewBatchSplits.length === 0) {
      setToastBanner({
        type: "error",
        text: "Số lượng chia lô dự kiến phải lớn hơn 0.",
      });
      return;
    }

    if (previewSplitsTotal > releaseSummary.remainingUnreleasedQty) {
      setToastBanner({
        type: "error",
        text: `Tổng số lượng chia lô (${fmtNum(previewSplitsTotal)} SP) vượt quá số lượng còn lại chưa phát hành (${fmtNum(releaseSummary.remainingUnreleasedQty)} SP).`,
      });
      return;
    }

    if (allowOverdriveInput && !overdriveJustificationInput.trim()) {
      setToastBanner({
        type: "error",
        text: "Khi kích hoạt chế độ chạy trước (Overdrive), bắt buộc phải nhập lý do giải trình kỹ thuật.",
      });
      return;
    }

    setIsGeneratingBatches(true);
    setToastBanner(null);
    try {
      const payload =
        splitMode === "CUSTOM"
          ? {
              orderItemId: currentLineItem.orderItemId,
              customBatchSplits: previewBatchSplits,
              allowOverdrive: allowOverdriveInput,
              justification: allowOverdriveInput
                ? overdriveJustificationInput.trim()
                : "Phát hành lệnh sản xuất chia lô tùy chỉnh.",
            }
          : {
              orderItemId: currentLineItem.orderItemId,
              maxBatchSize: Math.max(1, Number(maxBatchSizeInput) || releaseSummary.remainingUnreleasedQty),
              allowOverdrive: allowOverdriveInput,
              justification: allowOverdriveInput
                ? overdriveJustificationInput.trim()
                : "Phát hành lệnh sản xuất theo kích thước lô cấu hình.",
            };

      const res = await productionApi.generateBatches(payload);

      setToastBanner({
        type: res.goodsIssuedUnlocked || res.isOverdriver ? "success" : "info",
        text: res.message,
      });

      setIsGenerateModalOpen(false);
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể phát hành lệnh sản xuất.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsGeneratingBatches(false);
    }
  };

  // Mở modal Nghiệm thu KCS (chỉ REWORK_SUB_CARD dùng reinspect, còn MAIN_CARD và REPLENISH_LOT dùng qcInspect)
  const handleOpenQcModal = (step: BatchRoutingStepDto) => {
    if (!activeBatch) return;
    if (isReworkSubBatch) {
      setReinspectModalStep(step);
      setReinspectForm({
        batchStepId: step.batchStepId,
        inspectedQty: activeBatch.batchQty,
        passQty: activeBatch.batchQty,
        failQty: 0,
        notes: `KCS nghiệm thu lại thẻ nhánh phụ ${activeBatch.batchCode} tại ${step.stageName}.`,
      });
      return;
    }

    setQcModalStep(step);
    setQcForm({
      batchStepId: step.batchStepId,
      inspectedQty: activeBatch.batchQty,
      passQty: activeBatch.batchQty,
      failQty: 0,
      defectCode: "",
      defectName: "",
      disposition: "REWORK_SUB_CARD",
      notes: `KCS nghiệm thu cổng ${step.qualityGateCode || step.stageName}`,
    });
  };

  // Submit Nghiệm thu KCS Lô chính / Lô sản xuất bù
  const handleSubmitQcInspection = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeBatch || !qcModalStep) return;

    if (qcForm.passQty + qcForm.failQty !== qcForm.inspectedQty) {
      setToastBanner({
        type: "error",
        text: "Tổng Số lượng đạt và Số lượng lỗi phải bằng đúng toàn bộ sản lượng lô đang kiểm định.",
      });
      return;
    }

    setIsSubmittingAction(true);
    setToastBanner(null);
    try {
      const res = await productionApi.qcInspect(activeBatch.batchId, {
        ...qcForm,
        defectCode: qcForm.failQty > 0 ? qcForm.defectCode?.trim() || "DEF-ERR-01" : null,
        defectName:
          qcForm.failQty > 0
            ? qcForm.defectName?.trim() || "Lỗi gia công chi tiết cần sửa chữa"
            : null,
      });
      const scrapNote = res.requiresScrapReplenishment
        ? " Lô bù phế phẩm đang chờ phân bổ/xuất kho vật tư bổ sung."
        : "";
      setToastBanner({
        type: "success",
        text:
          (res.message ||
            `Đã ghi nhận kết quả KCS thành công! Đạt: ${res.passQty}, Lỗi: ${res.failQty}.`) +
          scrapNote,
      });
      setQcModalStep(null);
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể ghi nhận kết quả KCS.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsSubmittingAction(false);
    }
  };

  // Submit Nghiệm thu lại Lô phụ (Re-inspect Sub-batch)
  const handleSubmitReinspectSubBatch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeBatch || !reinspectModalStep) return;

    if (reinspectForm.passQty + reinspectForm.failQty !== reinspectForm.inspectedQty) {
      setToastBanner({
        type: "error",
        text: "Tổng Số lượng đạt và Số lượng lỗi phải khớp toàn bộ sản lượng thẻ nhánh phụ.",
      });
      return;
    }

    setIsSubmittingAction(true);
    setToastBanner(null);
    try {
      const res = await productionApi.reinspectSubBatch(activeBatch.batchId, reinspectForm);
      setToastBanner({
        type: res.passQty > 0 ? "success" : "info",
        text: res.message,
      });
      setReinspectModalStep(null);
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể nghiệm thu lại thẻ nhánh phụ.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsSubmittingAction(false);
    }
  };

  // Mở modal Chuyển công đoạn (Lô chính / Lô sản xuất bù) hoặc Chuyển tiếp độc lập (Lô phụ sửa chữa)
  const handleOpenAdvanceModal = (step: BatchRoutingStepDto) => {
    if (!activeBatch) return;
    if (isReworkSubBatch) {
      setContinueSubModalStep(step);
      setContinueSubNotesInput(
        `Cho phép thẻ nhánh phụ ${activeBatch.batchCode} chuyển tiếp độc lập sang công đoạn kế tiếp.`,
      );
      return;
    }

    const nextSeq = step.stageSequence + 1;
    setAdvanceModalStep(step);
    setAdvanceForm({
      targetStageSeq: nextSeq,
      notes: `Tổ trưởng chuyển từ công đoạn ${step.stageSequence} (${step.stageName}) sang công đoạn ${nextSeq}.`,
    });
  };

  // Submit Chuyển công đoạn Lô chính
  const handleSubmitAdvanceStep = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeBatch || !advanceModalStep) return;

    setIsSubmittingAction(true);
    setToastBanner(null);
    try {
      await productionApi.advanceStep(activeBatch.batchId, advanceForm);
      setToastBanner({
        type: "success",
        text: `Đã chuyển công đoạn thành công cho lô ${activeBatch.batchCode}!`,
      });
      setAdvanceModalStep(null);
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể chuyển công đoạn.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsSubmittingAction(false);
    }
  };

  // Submit Gộp lô phụ vào lô chính (Merge Lot)
  const handleSubmitMergeSubBatch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!mergeModalBatch) return;

    setIsSubmittingAction(true);
    setToastBanner(null);
    try {
      const res = await productionApi.mergeSubBatch(mergeModalBatch.batchId, {
        reason: mergeReasonInput.trim() || "Hoàn thành sửa chữa KCS, tái hòa nhập vào lô chính.",
      });
      setToastBanner({
        type: "success",
        text: res.message,
      });
      setMergeModalBatch(null);
      setSelectedBatchId(res.mainBatchId);
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể gộp lô phụ vào lô chính.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsSubmittingAction(false);
    }
  };

  // Submit Chuyển tiếp độc lập cho lô phụ
  const handleSubmitContinueSubIndependently = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeBatch || !continueSubModalStep) return;

    setIsSubmittingAction(true);
    setToastBanner(null);
    try {
      const res = await productionApi.continueSubBatchIndependently(activeBatch.batchId, {
        notes: continueSubNotesInput.trim() || undefined,
      });
      setToastBanner({
        type: "success",
        text: res.message,
      });
      setContinueSubModalStep(null);
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể chuyển tiếp độc lập thẻ nhánh phụ.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsSubmittingAction(false);
    }
  };

  // Submit Gửi yêu cầu Overdrive (Tổ trưởng)
  const handleSubmitRequestOverdrive = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!requestOverdriveBatch) return;

    if (!requestOverdriveForm.justification.trim()) {
      setToastBanner({
        type: "error",
        text: "Vui lòng nhập lý do giải trình kỹ thuật và rủi ro tiến độ ETD.",
      });
      return;
    }

    setIsSubmittingAction(true);
    setToastBanner(null);
    try {
      const res = await productionApi.requestOverdrive(
        requestOverdriveBatch.batchId,
        requestOverdriveForm,
      );
      setToastBanner({
        type: "info",
        text: res.message,
      });
      setRequestOverdriveBatch(null);
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể gửi yêu cầu Overdrive.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsSubmittingAction(false);
    }
  };

  // Submit Phê duyệt / Từ chối Overdrive (Chủ xưởng / Admin)
  const handleSubmitApproveOverdrive = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!approveOverdriveBatch) return;

    if (!approveOverdriveForm.approved && (!approveOverdriveForm.notes || approveOverdriveForm.notes.trim().length < 10)) {
      setToastBanner({
        type: "error",
        text: "Khi từ chối yêu cầu Overdrive, lý do từ chối phải có tối thiểu 10 ký tự.",
      });
      return;
    }

    setIsSubmittingAction(true);
    setToastBanner(null);
    try {
      const res = await productionApi.approveOverdrive(
        approveOverdriveBatch.batchId,
        approveOverdriveForm,
      );
      setToastBanner({
        type: approveOverdriveForm.approved ? "success" : "info",
        text: res.message,
      });
      setApproveOverdriveBatch(null);
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể xử lý phê duyệt Overdrive.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsSubmittingAction(false);
    }
  };

  // Submit Mở khóa chốt chặn Hold Point khi đã cấp phát phụ kiện (Thủ kho / Chủ xưởng / Admin)
  const handleUnlockHoldPoint = async (batch: ProductionBatchDto) => {
    setIsSubmittingAction(true);
    setToastBanner(null);
    try {
      const res = await productionApi.unlockHoldPoint(batch.batchId);
      setToastBanner({
        type: "success",
        text: res.message,
      });
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể mở khóa trạm dừng Hold Point.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsSubmittingAction(false);
    }
  };

  // Mở modal Nhập kho thành phẩm
  const handleOpenCompleteModal = (batch: ProductionBatchDto) => {
    setCompleteBatchModal(batch);
    setCompleteForm({
      receivedQty: batch.batchQty,
      warehouseLocation: "KHO-TP-A1",
      handoverNote: `Bàn giao nhập kho thành phẩm cho lô ${batch.batchCode} hoàn tất kiểm định đóng gói.`,
    });
  };

  // Submit Nhập kho thành phẩm
  const handleSubmitCompleteBatch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!completeBatchModal) return;

    setIsSubmittingAction(true);
    setToastBanner(null);
    try {
      const res = await productionApi.completeFinishedBatch(completeBatchModal.batchId, completeForm);
      setToastBanner({
        type: "success",
        text:
          res.message ||
          `Đã nhập kho thành phẩm thành công cho lô ${completeBatchModal.batchCode}!`,
      });
      setCompleteBatchModal(null);
      await loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể hoàn tất nhập kho thành phẩm.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsSubmittingAction(false);
    }
  };

  const activeBatchStatusMeta = getBatchStatusMeta(activeBatch?.batchStatus);
  const passedStepsCount = activeBatch
    ? activeBatch.steps.filter((s) => s.stepStatus === "PASSED").length
    : 0;
  const activeStepTimestamps =
    activeBatch && activeStepIndex >= 0
      ? getStepTimestamps(activeBatch.steps, activeStepIndex, activeBatch)
      : { startedAt: null, completedAt: null };

  return (
    <WmmsAppShell
      customTopLeft={
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => navigate("/orders")}
            className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-sm font-semibold text-slate-700 bg-white border border-slate-300 hover:bg-slate-50 transition shadow-2xs cursor-pointer"
          >
            <svg
              className="w-4 h-4 text-slate-500"
              fill="none"
              stroke="currentColor"
              strokeWidth={2}
              viewBox="0 0 24 24"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
            </svg>
            <span>Danh sách đơn hàng</span>
          </button>

          {activeSpaceKey && (
            <button
              type="button"
              onClick={() => navigate(`/orders/spaces/${encodeURIComponent(activeSpaceKey)}`)}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-semibold text-blue-700 bg-blue-50 border border-blue-200 hover:bg-blue-100 transition cursor-pointer"
            >
              <span>Không gian: {activeSpaceKey}</span>
            </button>
          )}

          {activeSpaceKey && canAccessBomPage && (
            <button
              type="button"
              onClick={() => navigate(`/bom?spaceKey=${encodeURIComponent(activeSpaceKey)}`)}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-semibold text-slate-700 bg-white border border-slate-300 hover:bg-slate-50 transition cursor-pointer"
            >
              <span>BOM &amp; quy trình</span>
            </button>
          )}

          {activeSpaceKey && canAccessReconciliationPage && (
            <button
              type="button"
              onClick={() =>
                navigate(`/inventory/reconciliation?spaceKey=${encodeURIComponent(activeSpaceKey)}`)
              }
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-semibold text-slate-700 bg-white border border-slate-300 hover:bg-slate-50 transition cursor-pointer"
            >
              <span>Đối chiếu tồn kho</span>
            </button>
          )}
        </div>
      }
    >
      <div className="max-w-[1440px] mx-auto space-y-6">
        {/* Toast thông báo kết quả */}
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

        {/* Lỗi không tìm thấy Không gian đơn hàng */}
        {!isLoadingSpace && (spaceError || !activeSpaceKey) && (
          <div className="px-5 py-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm font-semibold flex items-center justify-between gap-4 shadow-xs">
            <span>
              {spaceError ||
                "Vui lòng chọn Không gian đơn hàng từ danh sách để theo dõi tiến độ sản xuất."}
            </span>
            <button
              type="button"
              onClick={() => navigate("/orders")}
              className="px-3.5 py-1.5 bg-white hover:bg-rose-100 text-rose-700 border border-rose-300 text-xs font-bold rounded-lg shrink-0 cursor-pointer"
            >
              Về Danh sách đơn hàng
            </button>
          </div>
        )}

        {/* Header Thông tin Không gian & Lựa chọn Sản phẩm */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2.5">
              <span className="px-2.5 py-1 text-xs font-bold uppercase rounded-md bg-blue-50 text-blue-700 border border-blue-200 font-mono">
                {activeSpaceKey || "CHƯA CHỌN SPACE"}
              </span>
              {spaceInfo?.poNumber && (
                <span className="text-sm text-slate-600">
                  Mã PO: <strong className="font-mono text-slate-900">{spaceInfo.poNumber}</strong>
                </span>
              )}
              {spaceInfo?.customerName && (
                <span className="text-sm text-slate-600">
                  • Khách hàng:{" "}
                  <strong className="text-slate-900">{spaceInfo.customerName}</strong>
                </span>
              )}
              {spaceInfo?.etdDate && (
                <span className="text-sm text-slate-600">
                  • ETD: <strong className="text-slate-900">{fmtDate(spaceInfo.etdDate)}</strong>
                </span>
              )}
            </div>

            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight mt-2">
              Tiến độ &amp; Quy trình Sản xuất:{" "}
              <span className="text-blue-700">{currentLineItem?.productName || "Đang tải..."}</span>
            </h1>
            <p className="text-sm text-slate-600 mt-1">
              Mã SKU: <strong className="font-mono text-slate-800">{currentLineItem?.productSku || "—"}</strong>{" "}
              (Mục #{currentLineItem?.orderItemId}) • Theo dõi trực quan chuỗi công đoạn tuần tự theo cấu trúc định tuyến động (Routing) &amp; Kiểm soát KCS.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5 shrink-0">
            <button
              type="button"
              onClick={() => navigate(`/orders/spaces/${encodeURIComponent(activeSpaceKey)}`)}
              className="inline-flex items-center gap-1.5 px-3 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-bold rounded-lg transition cursor-pointer"
            >
              <span>← Về Không gian đơn hàng</span>
            </button>

            {currentLineItem && (
              <div className="px-3.5 py-2 rounded-lg bg-blue-50 border border-blue-200 text-xs text-blue-900">
                SL yêu cầu: <strong className="font-extrabold">{fmtNum(currentLineItem.orderedQty)} SP</strong>
              </div>
            )}
          </div>
        </div>

        {/* 4 THẺ KPI TỔNG QUAN TIẾN ĐỘ SẢN XUẤT */}
        {currentLineItem && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {/* Thẻ 1: Trạng thái lô / dây chuyền */}
            <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                TRẠNG THÁI LỆNH SẢN XUẤT
              </div>
              {activeBatch ? (
                <>
                  <div className="mt-2">
                    <span
                      className={`inline-block px-2.5 py-1 rounded-md text-xs font-extrabold uppercase ${activeBatchStatusMeta.badgeClass}`}
                    >
                      {activeBatchStatusMeta.label}
                    </span>
                  </div>
                  <div className="text-xs text-slate-500 mt-1.5">
                    Lô đang chọn: <strong className="font-mono text-slate-800">{activeBatch.batchCode}</strong>
                    {activeBatch.isOverdriver && " • Chế độ Overdrive"}
                  </div>
                </>
              ) : (
                <>
                  <div className="text-lg font-extrabold text-slate-700 mt-1.5">
                    CHƯA PHÁT HÀNH LỆNH
                  </div>
                  <div className="text-xs text-slate-500 mt-1">
                    {prerequisiteState.hasBomRecord
                      ? `Trạng thái BOM: ${prerequisiteState.bomStatusLabel}`
                      : "Chưa thiết lập định mức BOM"}
                  </div>
                </>
              )}
            </div>

            {/* Thẻ 2: Sản lượng đã phát hành / đặt hàng */}
            <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                SẢN LƯỢNG ĐÃ PHÁT HÀNH
              </div>
              <div className="text-2xl font-extrabold text-slate-900 mt-1.5">
                <span className="text-blue-600">{fmtNum(releaseSummary.initialMainReleasedQty)}</span>{" "}
                / {fmtNum(releaseSummary.orderedQty)} SP
              </div>
              <div className="text-xs text-slate-500 mt-1">
                {releaseSummary.mainBatchesCount} lô chính • {releaseSummary.subBatchesCount} lô phụ
                {releaseSummary.remainingUnreleasedQty > 0
                  ? ` • Còn ${fmtNum(releaseSummary.remainingUnreleasedQty)} SP chưa phát hành`
                  : " • Đã phát hành đủ"}
              </div>
            </div>

            {/* Thẻ 3: Tiến độ công đoạn lô hiện tại */}
            <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                TIẾN ĐỘ CÔNG ĐOẠN LÔ HIỆN TẠI
              </div>
              {activeBatch ? (
                <>
                  <div className="text-2xl font-extrabold text-slate-900 mt-1.5">
                    <span className="text-emerald-600">{passedStepsCount}</span> /{" "}
                    {activeBatch.steps.length} công đoạn
                  </div>
                  <div className="text-xs text-slate-500 mt-1 truncate">
                    {isBatchDone
                      ? activeBatch.batchQty === 0
                        ? "Lô đã đóng (0 SP đạt chuẩn — đã tách nhánh/hủy)"
                        : "Đã hoàn tất toàn bộ công đoạn và nhập kho thành phẩm"
                      : activeStep
                        ? `Hiện tại: S-${activeStep.stageSequence.toString().padStart(2, "0")} ${activeStep.stageName}`
                        : "Đang xử lý"}
                  </div>
                </>
              ) : (
                <>
                  <div className="text-2xl font-extrabold text-slate-900 mt-1.5">
                    0 / {productRoutingStages.length || "—"} công đoạn
                  </div>
                  <div className="text-xs text-slate-500 mt-1">
                    Quy trình định tuyến dự kiến của sản phẩm
                  </div>
                </>
              )}
            </div>

            {/* Thẻ 4: Hạn giao hàng (ETD) & Cảnh báo tiến độ */}
            <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                HẠN GIAO HÀNG (ETD) &amp; ĐỒNG BỘ
              </div>
              <div
                className={`text-xl font-extrabold mt-1.5 ${
                  orderProgress?.etdRiskLevel === "OVERDUE" || orderProgress?.etdRiskLevel === "WARNING"
                    ? "text-rose-600"
                    : "text-slate-900"
                }`}
              >
                {fmtDate(orderProgress?.etdDate || spaceInfo?.etdDate)}
              </div>
              <div className="text-xs text-slate-500 mt-1">
                {orderProgress
                  ? `Hoàn thành toàn đơn: ${orderProgress.overallCompletionPercent}% (${fmtNum(orderProgress.totalCompletedUnits)}/${fmtNum(orderProgress.totalOrderedUnits)} SP)`
                  : prerequisiteState.isMaterialIssuedToShop
                    ? "Vật tư đã xuất kho cấp phát ra xưởng"
                    : prerequisiteState.hasReservedAllocations
                      ? "Vật tư đã giữ chỗ, chưa xuất kho ra xưởng"
                      : "Chưa xác nhận xuất kho vật tư ra xưởng"}
              </div>
            </div>
          </div>
        )}

        {/* TRƯỜNG HỢP CHƯA PHÁT HÀNH LÔ SẢN XUẤT NÀO */}
        {!isLoadingBatches && currentLineItem && currentItemBatches.length === 0 && (
          <div className="bg-white rounded-xl border border-slate-200 p-8 shadow-xs space-y-6">
            <div className="text-center max-w-2xl mx-auto space-y-2">
              <div className="w-14 h-14 mx-auto rounded-full bg-blue-50 text-blue-600 flex items-center justify-center text-xl font-bold">
                ⚙️
              </div>
              <h3 className="text-lg font-bold text-slate-900">
                Sản phẩm &ldquo;{currentLineItem.productName}&rdquo; chưa phát hành lệnh sản xuất
              </h3>
              <p className="text-sm text-slate-600">
                Kiểm tra điều kiện tiên quyết trước khi chia lô và phát hành xuống phân xưởng. Lưu ý:
                Lệnh sản xuất sau khi phát hành không thể hủy bỏ.
              </p>
            </div>

            {/* Checklist Điều kiện tiên quyết */}
            <div className="max-w-3xl mx-auto grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
              <div
                className={`p-4 rounded-xl border ${
                  prerequisiteState.hasBomRecord && prerequisiteState.isBomApproved
                    ? "bg-emerald-50/70 border-emerald-200 text-emerald-900"
                    : prerequisiteState.hasBomRecord
                      ? "bg-amber-50/70 border-amber-200 text-amber-900"
                      : "bg-rose-50/70 border-rose-200 text-rose-900"
                }`}
              >
                <div className="font-bold uppercase tracking-wide flex items-center justify-between">
                  <span>1. Điều kiện Định mức BOM</span>
                  <span>
                    {prerequisiteState.hasBomRecord && prerequisiteState.isBomApproved
                      ? "✓ Đạt"
                      : prerequisiteState.hasBomRecord
                        ? "⚠ Chờ duyệt"
                        : "✕ Chưa có BOM"}
                  </span>
                </div>
                <div className="mt-1.5 text-slate-700">
                  {prerequisiteState.hasBomRecord
                    ? prerequisiteState.isBomApproved
                      ? `BOM đã được phê duyệt (${prerequisiteState.bomStatusLabel}), sẵn sàng phát hành.`
                      : `BOM đang ở trạng thái "${prerequisiteState.bomStatusLabel}". Cần duyệt BOM trước hoặc kích hoạt Overdrive có giải trình.`
                    : "Bắt buộc phải thiết lập định mức BOM trước khi phát hành lệnh sản xuất."}
                </div>
                {!prerequisiteState.isBomApproved && (
                  <button
                    type="button"
                    onClick={() => navigate(`/bom?spaceKey=${encodeURIComponent(activeSpaceKey)}`)}
                    className="mt-2 inline-flex items-center gap-1 font-bold text-blue-700 hover:underline cursor-pointer"
                  >
                    <span>Đi tới trang Lập &amp; Duyệt BOM →</span>
                  </button>
                )}
              </div>

              <div
                className={`p-4 rounded-xl border ${
                  prerequisiteState.isMaterialIssuedToShop
                    ? "bg-emerald-50/70 border-emerald-200 text-emerald-900"
                    : "bg-amber-50/70 border-amber-200 text-amber-900"
                }`}
              >
                <div className="font-bold uppercase tracking-wide flex items-center justify-between">
                  <span>2. Điều kiện Vật tư &amp; Xuất kho (BR-008)</span>
                  <span>{prerequisiteState.isMaterialIssuedToShop ? "✓ Đã xuất kho" : "⚠ Chưa xuất kho"}</span>
                </div>
                <div className="mt-1.5 text-slate-700">
                  {prerequisiteState.isMaterialIssuedToShop
                    ? "Toàn bộ vật tư đã được Thủ kho xuất ra xưởng. Lô phát hành sẽ chạy ngay."
                    : "Vật tư chưa chuyển trạng thái Xuất kho ra xưởng (ISSUED_TO_SHOP). Nếu không bật chạy trước Overdrive, lô tạo ra sẽ tạm khóa (LOCKED)."}
                </div>
                <button
                  type="button"
                  onClick={() =>
                    navigate(`/inventory/reconciliation?spaceKey=${encodeURIComponent(activeSpaceKey)}`)
                  }
                  className="mt-2 inline-flex items-center gap-1 font-bold text-blue-700 hover:underline cursor-pointer"
                >
                  <span>Kiểm tra Đối chiếu &amp; Khóa vật tư →</span>
                </button>
              </div>
            </div>

            {/* Hiển thị các công đoạn dự kiến từ quy trình động */}
            {productRoutingStages.length > 0 && (
              <div className="max-w-3xl mx-auto">
                <div className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-3">
                  Quy trình định tuyến dự kiến ({productRoutingStages.length} công đoạn):
                </div>
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-2 text-left">
                  {productRoutingStages.map((stg) => (
                    <div
                      key={stg.stageId || stg.stageSequence}
                      className="p-3 bg-slate-50 border border-slate-200 rounded-lg text-xs"
                    >
                      <span className="font-mono font-bold text-blue-600">
                        S-{stg.stageSequence.toString().padStart(2, "0")}
                      </span>
                      <div className="font-semibold text-slate-800 mt-1 truncate" title={stg.stageName}>
                        {stg.stageName}
                      </div>
                      <div className="text-slate-500 text-[11px] mt-0.5 flex items-center justify-between">
                        <span>{stg.workstationCode || "Trạm SX"}</span>
                        {stg.isHoldPoint && (
                          <span className="text-rose-600 font-bold">Hold Point</span>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="text-center pt-2">
              {isLeaderAuthorized ? (
                <button
                  type="button"
                  onClick={handleOpenGenerateModal}
                  disabled={!prerequisiteState.hasBomRecord}
                  className="px-6 py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-bold text-sm rounded-lg shadow-sm transition inline-flex items-center gap-2 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <span>Thiết lập Chia lô &amp; Phát hành Lệnh sản xuất</span>
                </button>
              ) : (
                <div className="text-xs text-slate-500 italic">
                  Chỉ Tổ trưởng sản xuất (Leader), Chủ xưởng hoặc Admin mới có quyền phát hành lệnh
                  sản xuất.
                </div>
              )}
            </div>
          </div>
        )}

        {/* TRƯỜNG HỢP ĐÃ CÓ LÔ SẢN XUẤT */}
        {currentItemBatches.length > 0 && activeBatch && (
          <div className="space-y-6">
            {/* Thanh chọn lô sản xuất + Nút phát hành bổ sung nếu chưa phát hành hết */}
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-2 overflow-x-auto pb-1">
                <span className="text-xs font-bold text-slate-500 uppercase shrink-0">
                  Danh sách lô ({currentItemBatches.length}):
                </span>
                {currentItemBatches.map((b) => {
                  const isSub = b.batchType === "REWORK_SUB_CARD" || b.batchType === "REPLENISH_LOT";
                  const isSelected = activeBatch.batchId === b.batchId;
                  const meta = getBatchStatusMeta(b.batchStatus);
                  return (
                    <button
                      key={b.batchId}
                      type="button"
                      onClick={() => setSelectedBatchId(b.batchId)}
                      className={`px-3 py-1.5 rounded-lg text-xs font-bold transition shrink-0 cursor-pointer flex items-center gap-1.5 border ${
                        isSelected
                          ? isSub
                            ? "bg-amber-600 text-white border-amber-700 shadow-2xs"
                            : "bg-blue-600 text-white border-blue-700 shadow-2xs"
                          : isSub
                            ? "bg-amber-50 text-amber-900 border-amber-300 hover:bg-amber-100"
                            : "bg-white text-slate-700 border-slate-300 hover:bg-slate-50"
                      }`}
                    >
                      {isSub && (
                        <span
                          className={`px-1 py-0.2 rounded text-[10px] font-extrabold ${
                            isSelected ? "bg-amber-800 text-amber-100" : "bg-amber-200 text-amber-900"
                          }`}
                        >
                          {(b as { batchTypeLabel?: string }).batchTypeLabel || "Lô phụ"}
                        </span>
                      )}
                      <span className="font-mono">{b.batchCode}</span>
                      <span className="opacity-85">({fmtNum(b.batchQty)} SP)</span>
                      <span
                        className={`w-2 h-2 rounded-full ${
                          b.batchStatus === "DONE"
                            ? "bg-emerald-400"
                            : b.batchStatus === "LOCKED"
                              ? "bg-amber-400"
                              : b.batchStatus === "HOLD_POINT_BLOCKED"
                                ? "bg-rose-500"
                                : "bg-sky-300"
                        }`}
                        title={meta.label}
                      />
                    </button>
                  );
                })}
              </div>

              {releaseSummary.remainingUnreleasedQty > 0 && isLeaderAuthorized && (
                <button
                  type="button"
                  onClick={handleOpenGenerateModal}
                  className="px-3.5 py-2 bg-blue-50 hover:bg-blue-100 text-blue-700 border border-blue-200 text-xs font-bold rounded-lg transition cursor-pointer shrink-0"
                >
                  + Phát hành bổ sung ({fmtNum(releaseSummary.remainingUnreleasedQty)} SP còn lại)
                </button>
              )}
            </div>

            {/* Banner thông tin riêng cho Lô phụ tách nhánh theo số lượng sản phẩm thực tế */}
            {isSubBatch && (
              <div className="p-4 rounded-xl bg-amber-50 border border-amber-300 text-amber-900 text-xs flex flex-wrap items-center justify-between gap-3">
                <div className="space-y-0.5">
                  <div className="font-extrabold uppercase tracking-wide flex items-center gap-2">
                    <span>
                      ⚠️ Thẻ nhánh phụ xử lý lỗi —{" "}
                      {(activeBatch as { batchTypeLabel?: string }).batchTypeLabel ||
                        (activeBatch.batchType === "REWORK"
                          ? "Lô sửa chữa"
                          : activeBatch.batchType === "SCRAP_REPLACEMENT"
                            ? "Lô bù phế phẩm"
                            : activeBatch.batchType)}{" "}
                      ({fmtNum(activeBatch.batchQty)} sản phẩm)
                    </span>
                  </div>
                  <div>
                    Tách từ Lô chính:{" "}
                    <strong className="font-mono">
                      {parentMainBatch?.batchCode || `#${activeBatch.parentBatchId}`}
                    </strong>{" "}
                    {parentMainBatch &&
                      `(${fmtNum(parentMainBatch.batchQty)} sản phẩm đạt • Đang ở công đoạn S-${parentMainBatch.currentStageSeq} • Trạng thái: ${(parentMainBatch as { batchStatusLabel?: string }).batchStatusLabel || getBatchStatusMeta(parentMainBatch.batchStatus).label})`}
                  </div>
                </div>
                {parentMainBatch && (
                  <button
                    type="button"
                    onClick={() => setSelectedBatchId(parentMainBatch.batchId)}
                    className="px-3 py-1.5 bg-white hover:bg-amber-100 text-amber-900 border border-amber-300 rounded-lg font-bold cursor-pointer"
                  >
                    Xem Lô chính ({parentMainBatch.batchCode})
                  </button>
                )}
              </div>
            )}

            {/* PHẦN 1: THANH TIẾN TRÌNH TRỰC QUAN (HORIZONTAL STEPPER ĐỘNG N BƯỚC) */}
            <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs space-y-4">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                    TIẾN TRÌNH SẢN XUẤT THEO QUY TRÌNH ĐỘNG
                  </div>
                  <h2 className="text-lg font-bold text-slate-900 mt-1 flex flex-wrap items-center gap-2">
                    <span>
                      Lô:{" "}
                      <span
                        className={`font-mono ${isSubBatch ? "text-amber-700" : "text-blue-600"}`}
                      >
                        {activeBatch.batchCode}
                      </span>
                    </span>
                    <span>• Số lượng:</span>
                    <strong>{fmtNum(activeBatch.batchQty)} SP</strong>
                    <span>•</span>
                    <span
                      className={`inline-block px-2.5 py-0.5 rounded-md text-xs font-bold uppercase ${activeBatchStatusMeta.badgeClass}`}
                    >
                      {activeBatchStatusMeta.label}
                    </span>
                    {activeBatch.isOverdriver && (
                      <span className="inline-block px-2 py-0.5 rounded-md text-xs font-bold uppercase bg-orange-100 text-orange-800 border border-orange-200">
                        ⚡ OVERDRIVE (Hold #{activeBatch.holdPointStageSeq ?? 5})
                      </span>
                    )}
                  </h2>
                </div>

                <div className="text-xs text-slate-500 text-right">
                  <div>
                    Tổng <strong>{activeBatch.steps.length} công đoạn</strong> định tuyến
                  </div>
                  <div>Khởi tạo: {fmtDateTime(activeBatch.createdAt)}</div>
                </div>
              </div>

              {/* Stepper Horizontal Scrollable */}
              <div className="overflow-x-auto pb-3 pt-2">
                <div className="flex items-start min-w-[760px] relative">
                  <div className="absolute top-6 left-8 right-8 h-1 bg-slate-200 -z-0" />

                  {activeBatch.steps.map((st) => {
                    const isPassed = st.stepStatus === "PASSED";
                    const isActiveStage =
                      !isBatchDone && st.stageSequence === activeBatch.currentStageSeq;
                    const isStepBlocked =
                      st.stepStatus === "BLOCKED" ||
                      (isActiveStage && (isBatchLocked || isBatchHoldBlocked));

                    return (
                      <div
                        key={st.batchStepId}
                        className="flex-1 flex flex-col items-center relative z-10 px-2"
                      >
                        {/* Circle Indicator */}
                        <div
                          className={`w-12 h-12 rounded-full flex items-center justify-center font-bold text-sm transition shadow-sm ${
                            isPassed
                              ? "bg-emerald-600 text-white ring-4 ring-emerald-100"
                              : isStepBlocked
                                ? "bg-amber-500 text-white ring-4 ring-amber-100"
                                : isActiveStage
                                  ? "bg-blue-600 text-white ring-4 ring-blue-100 animate-pulse"
                                  : "bg-white text-slate-400 border-2 border-slate-300"
                          }`}
                        >
                          {isPassed ? (
                            "✓"
                          ) : isStepBlocked ? (
                            "🔒"
                          ) : (
                            <span className="font-mono text-xs">
                              {st.stageSequence.toString().padStart(2, "0")}
                            </span>
                          )}
                        </div>

                        {/* Stage Info */}
                        <div className="text-center mt-2.5 space-y-0.5 max-w-[140px]">
                          <div
                            className={`text-xs font-bold truncate ${
                              isStepBlocked
                                ? "text-amber-800"
                                : isActiveStage
                                  ? "text-blue-700"
                                  : isPassed
                                    ? "text-emerald-800"
                                    : "text-slate-600"
                            }`}
                            title={st.stageName}
                          >
                            {st.stageName}
                          </div>
                          <div className="text-[11px] text-slate-500 font-mono">
                            {st.workstationCode}
                          </div>

                          <div className="text-[11px] font-semibold text-slate-700 mt-1">
                            <span className={isPassed ? "text-emerald-700 font-bold" : "text-slate-900"}>
                              {fmtNum(st.passedQty)}
                            </span>
                            <span className="text-slate-400">/{fmtNum(st.targetQty)}</span>
                          </div>

                          {st.isHoldPoint && (
                            <span className="inline-block mt-1 px-1.5 py-0.5 bg-rose-50 text-rose-700 border border-rose-200 text-[10px] font-bold rounded-sm">
                              Hold Point
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>

            {/* PHẦN 2: KHUNG HIGHLIGHTED THEO TRẠNG THÁI LÔ & NGƯỜI ĐƯỢC ỦY QUYỀN */}
            {activeStep && (
              <>
                {/* TRẠNG THÁI A: LÔ ĐÃ HOÀN TẤT (DONE) */}
                {isBatchDone && (
                  activeBatch.batchQty === 0 ? (
                    <div className="p-6 rounded-xl border-2 border-slate-300 bg-gradient-to-r from-slate-100 via-amber-50/40 to-white shadow-xs flex flex-col lg:flex-row lg:items-center justify-between gap-4">
                      <div className="space-y-1.5">
                        <span className="px-2.5 py-0.5 bg-slate-600 text-white text-xs font-extrabold uppercase rounded-full">
                          Lô đã đóng (0 SP đạt chuẩn)
                        </span>
                        <h3 className="text-xl font-extrabold text-slate-800">
                          Lô <span className="font-mono text-slate-700">{activeBatch.batchCode}</span>{" "}
                          đã đóng (0 SP đạt chuẩn — toàn bộ sản lượng đã tách nhánh phụ hoặc hủy phế phẩm)
                        </h3>
                        <p className="text-xs text-slate-600">
                          Vui lòng chọn thẻ nhánh phụ hoặc lô sản xuất bù tương ứng trên thanh chọn lô để tiếp tục theo dõi tiến độ.
                        </p>
                      </div>
                    </div>
                  ) : (
                    <div className="p-6 rounded-xl border-2 border-emerald-300 bg-gradient-to-r from-emerald-50 via-teal-50/50 to-white shadow-xs flex flex-col lg:flex-row lg:items-center justify-between gap-4">
                      <div className="space-y-1.5">
                        <span className="px-2.5 py-0.5 bg-emerald-600 text-white text-xs font-extrabold uppercase rounded-full">
                          ✓ Hoàn tất quy trình sản xuất
                        </span>
                        <h3 className="text-xl font-extrabold text-slate-900">
                          Lô <span className="font-mono text-emerald-700">{activeBatch.batchCode}</span>{" "}
                          đã hoàn tất toàn bộ các công đoạn &amp; nhập kho thành phẩm
                        </h3>
                        <p className="text-xs text-slate-600">
                          Sản lượng hoàn tất:{" "}
                          <strong className="text-emerald-800">{fmtNum(activeBatch.batchQty)} SP</strong>{" "}
                          • Hoàn thành lúc:{" "}
                          <strong>{fmtDateTime(activeStep.completedAt)}</strong>
                        </p>
                      </div>
                    </div>
                  )
                )}

                {/* TRẠNG THÁI B: LÔ ĐANG BỊ KHÓA (LOCKED hoặc HOLD_POINT_BLOCKED) */}
                {!isBatchDone && (isBatchLocked || isBatchHoldBlocked) && (
                  <div
                    className={`p-6 rounded-xl border-2 shadow-xs space-y-4 ${
                      isBatchHoldBlocked
                        ? "bg-gradient-to-r from-rose-50 via-orange-50/40 to-white border-rose-400"
                        : "bg-gradient-to-r from-amber-50 via-yellow-50/40 to-white border-amber-400"
                    }`}
                  >
                    <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
                      <div className="space-y-1.5">
                        <div className="flex flex-wrap items-center gap-2">
                          <span
                            className={`px-2.5 py-0.5 text-white text-xs font-extrabold uppercase rounded-full ${
                              isBatchHoldBlocked ? "bg-rose-600" : "bg-amber-600"
                            }`}
                          >
                            {isBatchHoldBlocked
                              ? `⛔ KHÓA CỨNG TẠI HOLD POINT (S-${activeBatch.holdPointStageSeq ?? activeStep.stageSequence})`
                              : "🔒 ĐANG TẠM KHÓA CHỜ XUẤT KHO VẬT TƯ (LOCKED)"}
                          </span>
                          <span className="text-xs font-bold text-slate-600">
                            Công đoạn đang chờ: S-{activeStep.stageSequence.toString().padStart(2, "0")}{" "}
                            {activeStep.stageName}
                          </span>
                        </div>

                        <h3 className="text-lg font-extrabold text-slate-900">
                          {isBatchHoldBlocked
                            ? "Không thể tiếp tục qua Trạm dừng kỹ thuật do thiếu xuất kho phụ kiện / sơn"
                            : "Lô sản xuất chưa được mở khóa để kiểm định KCS hoặc gia công"}
                        </h3>
                        <p className="text-xs text-slate-700">
                          {isBatchHoldBlocked
                            ? "Theo quy tắc QT-06 / KBN02, Thủ kho cần xác nhận xuất kho bổ sung vật tư (Mở khóa Hold Point) hoặc Chủ xưởng phê duyệt ghi đè ngoại lệ Overdrive để dây chuyền tiếp tục."
                            : "Theo quy tắc BR-008, vật tư chưa được chuyển trạng thái Xuất kho ra xưởng (ISSUED_TO_SHOP). Tổ trưởng có thể gửi yêu cầu chạy trước Overdrive hoặc Chủ xưởng/Admin phê duyệt mở khóa để bắt đầu gia công ngay."}
                        </p>
                      </div>

                      <div className="flex flex-wrap items-center gap-2.5 shrink-0">
                        {isLeaderAuthorized && !isOwnerOrAdmin && (
                          <button
                            type="button"
                            onClick={() => {
                              setRequestOverdriveBatch(activeBatch);
                              setRequestOverdriveForm({
                                holdPointStageSeq: activeBatch.holdPointStageSeq ?? 5,
                                justification:
                                  "Đề nghị mở khóa chạy sơ chế gỗ thô trước để đảm bảo tiến độ giao hàng ETD.",
                              });
                            }}
                            className="px-4 py-2.5 bg-amber-600 hover:bg-amber-700 text-white text-xs font-bold rounded-lg shadow-sm transition cursor-pointer"
                          >
                            Gửi yêu cầu Mở khóa (Overdrive)
                          </button>
                        )}

                        {isOwnerOrAdmin && (
                          <button
                            type="button"
                            onClick={() => {
                              setApproveOverdriveBatch(activeBatch);
                              setApproveOverdriveForm({
                                approved: true,
                                holdPointStageSeq: activeBatch.holdPointStageSeq ?? 5,
                                notes:
                                  "Chủ xưởng phê duyệt mở khóa chạy sản xuất trước trạm dừng Hold Point.",
                              });
                            }}
                            className="px-4 py-2.5 bg-amber-600 hover:bg-amber-700 text-white text-xs font-bold rounded-lg shadow-sm transition cursor-pointer"
                          >
                            Phê duyệt Mở khóa Sản xuất (Overdrive)
                          </button>
                        )}

                        {(isWarehouseAuthorized || isOwnerOrAdmin) &&
                          (isBatchHoldBlocked || !activeBatch.goodsIssuedUnlocked) && (
                            <button
                              type="button"
                              disabled={isSubmittingAction}
                              onClick={() => handleUnlockHoldPoint(activeBatch)}
                              className="px-4 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold rounded-lg shadow-sm transition cursor-pointer disabled:opacity-50"
                            >
                              Xác nhận Xuất kho &amp; Mở khóa Hold Point
                            </button>
                          )}
                      </div>
                    </div>
                  </div>
                )}

                {/* TRẠNG THÁI C: LÔ ĐANG CHẠY BÌNH THƯỜNG (IN_PROGRESS / QC_PENDING) */}
                {!isBatchDone && !isBatchLocked && !isBatchHoldBlocked && (
                  <div
                    className={`p-6 rounded-xl border-2 transition shadow-xs ${
                      isQcAuthorized || isLeaderAuthorized || (isFinalStep && isWarehouseAuthorized)
                        ? isSubBatch
                          ? "bg-gradient-to-r from-amber-50/90 via-orange-50/40 to-white border-amber-400 ring-2 ring-amber-100"
                          : "bg-gradient-to-r from-blue-50/80 via-indigo-50/50 to-white border-blue-400 ring-2 ring-blue-100"
                        : "bg-slate-50 border-slate-300"
                    }`}
                  >
                    <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
                      <div className="space-y-1.5">
                        <div className="flex flex-wrap items-center gap-2">
                          <span
                            className={`px-2.5 py-0.5 text-white text-xs font-extrabold uppercase rounded-full ${
                              isSubBatch ? "bg-amber-600" : "bg-blue-600"
                            }`}
                          >
                            Công đoạn đang thực thi: S-
                            {activeStep.stageSequence.toString().padStart(2, "0")}
                          </span>
                          <span className="text-xs font-bold text-slate-600 uppercase">
                            Trạm:{" "}
                            <strong className="text-slate-900">
                              {activeStep.workstationName || activeStep.workstationCode}
                            </strong>
                          </span>
                          {activeStep.isHoldPoint && (
                            <span className="px-2 py-0.5 bg-rose-600 text-white text-xs font-bold rounded-sm">
                              ⛔ Điểm dừng QC bắt buộc (Hold Point)
                            </span>
                          )}
                        </div>

                        <h3 className="text-xl font-extrabold text-slate-900">
                          {activeStep.stageName} •{" "}
                          <span className="text-blue-700">
                            {isCurrentStepPassed
                              ? isFinalStep
                                ? "ĐÃ ĐẠT KCS XUẤT XƯỞNG (CHỜ NHẬP KHO THÀNH PHẨM)"
                                : "ĐÃ HOÀN TẤT KCS (SẴN SÀNG CHUYỂN BƯỚC)"
                              : "ĐANG CHỜ NGHIỆM THU CHẤT LƯỢNG KCS"}
                          </span>
                        </h3>

                        <p className="text-xs text-slate-600">
                          Tiêu chuẩn KCS ({activeStep.qualityGateCode || "QC GATE"}):{" "}
                          <strong>
                            {activeStep.inspectionCriteria ||
                              "Kiểm định toàn bộ kích thước & thông số kỹ thuật phôi"}
                          </strong>
                        </p>

                        <div className="flex flex-wrap items-center gap-4 text-xs text-slate-600 pt-0.5">
                          <span>
                            🕒 Bắt đầu:{" "}
                            <strong className="text-slate-800">
                              {activeStepTimestamps.startedAt
                                ? fmtDateTime(activeStepTimestamps.startedAt)
                                : "Chưa ghi nhận"}
                            </strong>
                          </span>
                          <span>
                            🏁 Kết thúc:{" "}
                            <strong className="text-slate-800">
                              {activeStepTimestamps.completedAt
                                ? fmtDateTime(activeStepTimestamps.completedAt)
                                : "Đang thực hiện"}
                            </strong>
                          </span>
                        </div>
                      </div>

                      {/* Cụm nút Thao tác ủy quyền */}
                      <div className="flex flex-wrap items-center gap-3 shrink-0">
                        {/* Trường hợp 1: Chưa đạt 100% KCS */}
                        {!isCurrentStepPassed && isQcAuthorized && (
                          <button
                            type="button"
                            onClick={() => handleOpenQcModal(activeStep)}
                            className="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-sm rounded-lg shadow-sm transition inline-flex items-center gap-2 cursor-pointer"
                          >
                            <svg
                              className="w-4 h-4"
                              fill="none"
                              stroke="currentColor"
                              strokeWidth={2}
                              viewBox="0 0 24 24"
                            >
                              <path
                                strokeLinecap="round"
                                strokeLinejoin="round"
                                d="M5 13l4 4L19 7"
                              />
                            </svg>
                            <span>
                              {isReworkSubBatch
                                ? "Nghiệm thu lại Lô phụ (KCS)"
                                : "Nghiệm thu KCS (Cổng QC)"}
                            </span>
                          </button>
                        )}

                        {/* Trường hợp 2A: Lô phụ đã đạt KCS và có thể Gộp lô chính */}
                        {isSubBatch && canMergeWithParent && isLeaderAuthorized && parentMainBatch && (
                          <button
                            type="button"
                            onClick={() => {
                              setMergeModalBatch(activeBatch);
                              setMergeReasonInput(
                                `Gộp ${activeBatch.batchQty} SP đã sửa chữa đạt chuẩn vào lô chính ${parentMainBatch.batchCode} tại công đoạn S-${activeBatch.currentStageSeq}.`,
                              );
                            }}
                            className="px-5 py-2.5 bg-amber-600 hover:bg-amber-700 text-white font-bold text-sm rounded-lg shadow-sm transition inline-flex items-center gap-2 cursor-pointer"
                          >
                            <span>Gộp vào Lô chính ({parentMainBatch.batchCode})</span>
                          </button>
                        )}

                        {/* Trường hợp 2B: Đã đạt KCS và chưa phải công đoạn cuối -> Chuyển bước */}
                        {isCurrentStepPassed && !isFinalStep && isLeaderAuthorized && (
                          <button
                            type="button"
                            onClick={() => handleOpenAdvanceModal(activeStep)}
                            className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-bold text-sm rounded-lg shadow-sm transition inline-flex items-center gap-2 cursor-pointer"
                          >
                            <span>
                              {isReworkSubBatch
                                ? `Chuyển bước độc lập S-${(activeStep.stageSequence + 1).toString().padStart(2, "0")} →`
                                : `Chuyển sang công đoạn S-${(activeStep.stageSequence + 1).toString().padStart(2, "0")} →`}
                            </span>
                          </button>
                        )}

                        {/* Trường hợp 3: Công đoạn cuối cùng đã đạt KCS -> Nhập kho thành phẩm */}
                        {isCurrentStepPassed &&
                          isFinalStep &&
                          (isLeaderAuthorized || isWarehouseAuthorized) && (
                            <button
                              type="button"
                              onClick={() => handleOpenCompleteModal(activeBatch)}
                              className="px-5 py-2.5 bg-purple-600 hover:bg-purple-700 text-white font-bold text-sm rounded-lg shadow-sm transition inline-flex items-center gap-2 cursor-pointer"
                            >
                              <span>Xác nhận Hoàn tất &amp; Nhập kho Thành phẩm ★</span>
                            </button>
                          )}

                        {/* Mở khóa Hold Point sớm nếu là lô Overdrive chưa xuất phụ kiện */}
                        {activeBatch.isOverdriver &&
                          !activeBatch.goodsIssuedUnlocked &&
                          (isWarehouseAuthorized || isOwnerOrAdmin) && (
                            <button
                              type="button"
                              disabled={isSubmittingAction}
                              onClick={() => handleUnlockHoldPoint(activeBatch)}
                              className="px-4 py-2.5 bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-300 font-bold text-xs rounded-lg transition cursor-pointer disabled:opacity-50"
                            >
                              Xác nhận Xuất kho phụ kiện
                            </button>
                          )}

                        <button
                          type="button"
                          onClick={() =>
                            setDetailModalContext({
                              step: activeStep,
                              startedAt: activeStepTimestamps.startedAt,
                              completedAt: activeStepTimestamps.completedAt,
                            })
                          }
                          className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 font-semibold text-sm rounded-lg border border-slate-300 transition cursor-pointer"
                        >
                          Chi tiết công đoạn
                        </button>
                      </div>
                    </div>

                    {/* Progress bar của bước hiện tại */}
                    <div className="mt-4 pt-3 border-t border-slate-200/80">
                      <div className="flex items-center justify-between text-xs text-slate-600 mb-1">
                        <span>
                          Số lượng nghiệm thu:{" "}
                          <strong className="text-emerald-700">
                            {fmtNum(activeStep.passedQty)} đạt
                          </strong>{" "}
                          •{" "}
                          <strong className="text-rose-700">
                            {fmtNum(activeStep.defectQty)} lỗi
                          </strong>{" "}
                          / Tổng định mức <strong>{fmtNum(activeStep.targetQty)} SP</strong>
                        </span>
                        <span className="font-bold text-slate-800">
                          {Math.min(
                            100,
                            Math.round(
                              ((activeStep.passedQty + activeStep.defectQty) /
                                (activeStep.targetQty || 1)) *
                                100,
                            ),
                          )}
                          %
                        </span>
                      </div>
                      <div className="w-full h-2.5 bg-slate-200 rounded-full overflow-hidden flex">
                        <div
                          className="bg-emerald-600 h-full transition-all duration-300"
                          style={{
                            width: `${Math.min(100, Math.round((activeStep.passedQty / (activeStep.targetQty || 1)) * 100))}%`,
                          }}
                        />
                        <div
                          className="bg-rose-500 h-full transition-all duration-300"
                          style={{
                            width: `${Math.min(100, Math.round((activeStep.defectQty / (activeStep.targetQty || 1)) * 100))}%`,
                          }}
                        />
                      </div>
                    </div>
                  </div>
                )}
              </>
            )}

            {/* PHẦN 3: BẢNG CHI TIẾT CÁC CÔNG ĐOẠN ĐỊNH TUYẾN (STAGES TABLE) */}
            <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
              <div className="p-5 border-b border-slate-200 flex flex-wrap items-center justify-between gap-3 bg-slate-50/50">
                <div>
                  <h3 className="text-base font-bold text-slate-900">
                    Chi tiết các công đoạn sản xuất &amp; Cổng QC
                  </h3>
                  <p className="text-xs text-slate-500 mt-0.5">
                    Bảng theo dõi tuần tự qua các trạm máy, thời gian bắt đầu/kết thúc, kiểm định
                    chất lượng và trạng thái thực thi.
                  </p>
                </div>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="bg-slate-50 text-slate-500 uppercase text-[11px] font-bold tracking-wider border-b border-slate-200">
                    <tr>
                      <th className="px-5 py-3">Thứ tự &amp; Công đoạn</th>
                      <th className="px-4 py-3">Trạm gia công</th>
                      <th className="px-4 py-3">Cổng QC Gate &amp; Tiêu chuẩn</th>
                      <th className="px-4 py-3">Thời gian (Bắt đầu / Kết thúc)</th>
                      <th className="px-4 py-3 text-right">Số lượng Đạt / Tổng</th>
                      <th className="px-4 py-3 text-center">Trạng thái</th>
                      <th className="px-5 py-3 text-right">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200">
                    {activeBatch.steps.map((st, idx) => {
                      const isPassed = st.stepStatus === "PASSED";
                      const isActiveStage =
                        !isBatchDone && st.stageSequence === activeBatch.currentStageSeq;
                      const isStepFinal =
                        st.stageSequence ===
                        Math.max(...activeBatch.steps.map((x) => x.stageSequence));
                      const canActOnRow =
                        isActiveStage && !isBatchLocked && !isBatchHoldBlocked && !isBatchDone;
                      const timestamps = getStepTimestamps(activeBatch.steps, idx, activeBatch);
                      const statusMeta = getStepStatusMeta(st, isActiveStage, activeBatch.batchStatus);

                      return (
                        <tr
                          key={st.batchStepId}
                          className={`hover:bg-slate-50/80 transition ${
                            isActiveStage ? "bg-blue-50/40 font-medium" : ""
                          }`}
                        >
                          <td className="px-5 py-4">
                            <div className="flex items-center gap-3">
                              <span
                                className={`w-7 h-7 rounded-full flex items-center justify-center font-bold text-xs shrink-0 ${
                                  isPassed
                                    ? "bg-emerald-100 text-emerald-800"
                                    : isActiveStage
                                      ? isBatchLocked || isBatchHoldBlocked
                                        ? "bg-amber-500 text-white"
                                        : "bg-blue-600 text-white"
                                      : "bg-slate-100 text-slate-600"
                                }`}
                              >
                                {isPassed ? "✓" : st.stageSequence}
                              </span>
                              <div>
                                <div className="font-bold text-slate-900">{st.stageName}</div>
                                {st.isHoldPoint && (
                                  <span className="text-[11px] font-bold text-rose-600 flex items-center gap-1">
                                    ⛔ Chốt chặn Hold Point
                                  </span>
                                )}
                              </div>
                            </div>
                          </td>

                          <td className="px-4 py-4">
                            <div className="text-slate-800 font-semibold">
                              {st.workstationName || st.workstationCode}
                            </div>
                            <div className="text-xs text-slate-500 font-mono">
                              {st.workstationCode}
                            </div>
                          </td>

                          <td className="px-4 py-4 max-w-xs">
                            <div className="text-xs font-bold text-slate-800">
                              {st.qualityGateName || st.qualityGateCode}
                            </div>
                            <div
                              className="text-xs text-slate-500 truncate"
                              title={st.inspectionCriteria}
                            >
                              {st.inspectionCriteria || "Kiểm định toàn bộ thông số kỹ thuật"}
                            </div>
                          </td>

                          <td className="px-4 py-4 text-xs whitespace-nowrap">
                            <div className="space-y-1">
                              <div className="text-slate-700">
                                <span className="text-slate-400">Bắt đầu:</span>{" "}
                                <strong className="font-mono text-slate-800">
                                  {timestamps.startedAt ? fmtDateTime(timestamps.startedAt) : "Chưa bắt đầu"}
                                </strong>
                              </div>
                              <div className="text-slate-700">
                                <span className="text-slate-400">Kết thúc:</span>{" "}
                                <strong
                                  className={`font-mono ${
                                    timestamps.completedAt ? "text-emerald-700" : "text-slate-500"
                                  }`}
                                >
                                  {timestamps.completedAt
                                    ? fmtDateTime(timestamps.completedAt)
                                    : timestamps.startedAt
                                      ? "Đang thực hiện"
                                      : "—"}
                                </strong>
                              </div>
                            </div>
                          </td>

                          <td className="px-4 py-4 text-right whitespace-nowrap">
                            <div className="font-bold text-slate-900">
                              <span className="text-emerald-700">{fmtNum(st.passedQty)}</span> /{" "}
                              {fmtNum(st.targetQty)} SP
                            </div>
                            {st.defectQty > 0 && (
                              <div className="text-xs text-rose-600 font-semibold">
                                ({fmtNum(st.defectQty)} phôi lỗi)
                              </div>
                            )}
                          </td>

                          <td className="px-4 py-4 text-center whitespace-nowrap">
                            <span
                              className={`inline-block px-2.5 py-1 rounded-md text-xs font-bold uppercase ${statusMeta.badgeClass}`}
                            >
                              {statusMeta.label}
                            </span>
                          </td>

                          <td className="px-5 py-4 text-right space-x-2 whitespace-nowrap">
                            {canActOnRow && isQcAuthorized && !isPassed && (
                              <button
                                type="button"
                                onClick={() => handleOpenQcModal(st)}
                                className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold rounded-lg transition cursor-pointer"
                              >
                                {isReworkSubBatch ? "KCS lại" : "KCS"}
                              </button>
                            )}

                            {canActOnRow &&
                              isPassed &&
                              isLeaderAuthorized &&
                              !isStepFinal && (
                                <button
                                  type="button"
                                  onClick={() => handleOpenAdvanceModal(st)}
                                  className="px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-lg transition cursor-pointer"
                                >
                                  {isReworkSubBatch ? "Đi tiếp →" : "Chuyển bước →"}
                                </button>
                              )}

                            {canActOnRow &&
                              isPassed &&
                              isStepFinal &&
                              (isLeaderAuthorized || isWarehouseAuthorized) && (
                                <button
                                  type="button"
                                  onClick={() => handleOpenCompleteModal(activeBatch)}
                                  className="px-3 py-1.5 bg-purple-600 hover:bg-purple-700 text-white text-xs font-bold rounded-lg transition cursor-pointer"
                                >
                                  Nhập kho TP
                                </button>
                              )}

                            <button
                              type="button"
                              onClick={() =>
                                setDetailModalContext({
                                  step: st,
                                  startedAt: timestamps.startedAt,
                                  completedAt: timestamps.completedAt,
                                })
                              }
                              className="px-2.5 py-1.5 text-xs text-slate-600 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 rounded-lg transition cursor-pointer"
                            >
                              Chi tiết
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}

        {/* MODAL 1: THIẾT LẬP CHIA LÔ & PHÁT HÀNH LỆNH SẢN XUẤT */}
        {isGenerateModalOpen && currentLineItem && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4 overflow-y-auto">
            <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-4 my-8">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <div>
                  <h3 className="text-base font-bold text-slate-900">
                    Thiết lập Chia lô &amp; Phát hành Lệnh sản xuất
                  </h3>
                  <p className="text-xs text-slate-500 mt-0.5">
                    {currentLineItem.productName} ({currentLineItem.productSku}) • Số lượng còn lại
                    cần phát hành:{" "}
                    <strong className="text-blue-700">
                      {fmtNum(releaseSummary.remainingUnreleasedQty)} SP
                    </strong>
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setIsGenerateModalOpen(false)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleSubmitGenerateBatches} className="space-y-4 text-sm">
                {/* Chọn chế độ chia lô */}
                <div className="flex items-center gap-2 bg-slate-100 p-1 rounded-xl">
                  <button
                    type="button"
                    onClick={() => setSplitMode("MAX_SIZE")}
                    className={`flex-1 py-2 text-xs font-bold rounded-lg transition cursor-pointer ${
                      splitMode === "MAX_SIZE"
                        ? "bg-white text-blue-700 shadow-2xs"
                        : "text-slate-600 hover:text-slate-900"
                    }`}
                  >
                    Chia tự động theo Kích thước lô (BatchSize)
                  </button>
                  <button
                    type="button"
                    onClick={() => setSplitMode("CUSTOM")}
                    className={`flex-1 py-2 text-xs font-bold rounded-lg transition cursor-pointer ${
                      splitMode === "CUSTOM"
                        ? "bg-white text-blue-700 shadow-2xs"
                        : "text-slate-600 hover:text-slate-900"
                    }`}
                  >
                    Chia lô tùy chỉnh / Phát hành từng đợt
                  </button>
                </div>

                {splitMode === "MAX_SIZE" ? (
                  <div className="space-y-3 p-4 bg-slate-50 border border-slate-200 rounded-xl">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <label className="text-xs font-bold text-slate-700">
                        Kích thước tối đa mỗi lô (MaxBatchSize):
                      </label>
                      <div className="flex flex-wrap gap-1.5">
                        <button
                          type="button"
                          onClick={() => setMaxBatchSizeInput(releaseSummary.remainingUnreleasedQty)}
                          className="px-2.5 py-1 bg-blue-50 hover:bg-blue-100 text-blue-700 border border-blue-200 rounded text-[11px] font-bold cursor-pointer"
                        >
                          Gộp 1 lô ({fmtNum(releaseSummary.remainingUnreleasedQty)} SP)
                        </button>
                        {[100, 200, 250].map((preset) => (
                          <button
                            key={preset}
                            type="button"
                            onClick={() => setMaxBatchSizeInput(preset)}
                            className="px-2.5 py-1 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 rounded text-[11px] font-semibold cursor-pointer"
                          >
                            {preset} SP/lô
                          </button>
                        ))}
                      </div>
                    </div>
                    <input
                      type="number"
                      min={1}
                      max={10000}
                      value={maxBatchSizeInput}
                      onChange={(e) => setMaxBatchSizeInput(Number(e.target.value))}
                      className="w-full px-3 py-2 border border-slate-300 bg-white rounded-lg font-bold text-slate-900"
                      required
                    />
                  </div>
                ) : (
                  <div className="space-y-3 p-4 bg-slate-50 border border-slate-200 rounded-xl">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-bold text-slate-700">
                        Nhập số lượng cho từng lô (có thể phát hành một phần):
                      </span>
                      <button
                        type="button"
                        onClick={() => setCustomSplitsInput((prev) => [...prev, 50])}
                        className="px-2.5 py-1 bg-blue-600 hover:bg-blue-700 text-white rounded text-xs font-bold cursor-pointer"
                      >
                        + Thêm lô
                      </button>
                    </div>
                    <div className="space-y-2 max-h-40 overflow-y-auto pr-1">
                      {customSplitsInput.map((qty, idx) => (
                        <div key={idx} className="flex items-center gap-2">
                          <span className="text-xs font-mono font-bold text-slate-500 w-16">
                            Lô #{idx + 1}:
                          </span>
                          <input
                            type="number"
                            min={1}
                            max={releaseSummary.remainingUnreleasedQty}
                            value={qty}
                            onChange={(e) => {
                              const next = [...customSplitsInput];
                              next[idx] = Number(e.target.value);
                              setCustomSplitsInput(next);
                            }}
                            className="flex-1 px-3 py-1.5 border border-slate-300 bg-white rounded-lg font-bold text-xs"
                            required
                          />
                          {customSplitsInput.length > 1 && (
                            <button
                              type="button"
                              onClick={() =>
                                setCustomSplitsInput((prev) => prev.filter((_, i) => i !== idx))
                              }
                              className="px-2.5 py-1.5 text-xs font-bold text-rose-600 hover:bg-rose-50 rounded-lg cursor-pointer"
                            >
                              Xóa
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Live Preview kết quả chia lô */}
                <div className="p-3.5 bg-blue-50/70 border border-blue-200 rounded-xl space-y-2">
                  <div className="flex items-center justify-between text-xs font-bold text-blue-900">
                    <span>Xem trước danh sách lô sẽ tạo ({previewBatchSplits.length} lô):</span>
                    <span
                      className={
                        previewSplitsTotal > releaseSummary.remainingUnreleasedQty
                          ? "text-rose-600"
                          : "text-blue-800"
                      }
                    >
                      Tổng phát hành đợt này: {fmtNum(previewSplitsTotal)} /{" "}
                      {fmtNum(releaseSummary.remainingUnreleasedQty)} SP
                    </span>
                  </div>
                  <div className="flex flex-wrap gap-1.5">
                    {previewBatchSplits.map((sz, i) => (
                      <span
                        key={i}
                        className="px-2.5 py-1 bg-white border border-blue-200 rounded-md text-xs font-bold text-slate-800"
                      >
                        Lô {releaseSummary.mainBatchesCount + i + 1}:{" "}
                        <span className="text-blue-700">{fmtNum(sz)} SP</span>
                      </span>
                    ))}
                  </div>
                </div>

                {/* Tùy chọn Overdrive */}
                <div className="p-4 rounded-xl border border-amber-200 bg-amber-50/60 space-y-2.5">
                  <label className="flex items-start gap-2.5 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={allowOverdriveInput}
                      onChange={(e) => setAllowOverdriveInput(e.target.checked)}
                      className="mt-0.5 w-4 h-4 rounded border-amber-400 text-amber-600"
                    />
                    <div className="text-xs">
                      <span className="font-bold text-amber-900">
                        Kích hoạt chế độ Chạy trước (Overdrive) để mở khóa sản xuất ngay
                      </span>
                      <p className="text-slate-600 mt-0.5">
                        {prerequisiteState.isMaterialIssuedToShop
                          ? "Vật tư đã xuất kho đầy đủ, không cần bật Overdrive."
                          : "Do chưa xác nhận Xuất kho vật tư ra xưởng, nếu KHÔNG tích chọn mục này, các lô tạo ra sẽ ở trạng thái TẠM KHÓA (LOCKED) cho đến khi mở khóa."}
                      </p>
                    </div>
                  </label>

                  {allowOverdriveInput && (
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1">
                        Lý do giải trình kỹ thuật (bắt buộc):
                      </label>
                      <textarea
                        rows={2}
                        maxLength={500}
                        value={overdriveJustificationInput}
                        onChange={(e) => setOverdriveJustificationInput(e.target.value)}
                        className="w-full px-3 py-2 border border-amber-300 bg-white rounded-lg text-xs"
                        placeholder="Nhập lý do chạy trước (tối đa 500 ký tự)..."
                        required
                      />
                    </div>
                  )}
                </div>

                <div className="p-3 bg-rose-50 border border-rose-200 rounded-lg text-xs text-rose-800">
                  <strong>Lưu ý quan trọng:</strong> Lệnh sản xuất sau khi phát hành xuống phân
                  xưởng <strong>không thể hủy bỏ</strong> và đơn hàng sẽ chuyển sang trạng thái Đang
                  sản xuất.
                </div>

                <div className="flex items-center justify-end gap-3 pt-2 border-t border-slate-200">
                  <button
                    type="button"
                    onClick={() => setIsGenerateModalOpen(false)}
                    className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    disabled={
                      isGeneratingBatches ||
                      previewBatchSplits.length === 0 ||
                      previewSplitsTotal > releaseSummary.remainingUnreleasedQty
                    }
                    className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50"
                  >
                    {isGeneratingBatches ? "Đang phát hành..." : "Xác nhận Phát hành Lệnh sản xuất"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL 2: NGHIỆM THU KCS LÔ CHÍNH (TUÂN THỦ 100% RULE BR-008) */}
        {qcModalStep && activeBatch && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <h3 className="text-base font-bold text-slate-900">
                  Nghiệm thu KCS • S-{qcModalStep.stageSequence} ({qcModalStep.stageName})
                </h3>
                <button
                  type="button"
                  onClick={() => setQcModalStep(null)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleSubmitQcInspection} className="space-y-4 text-sm">
                <div className="p-3 bg-blue-50 rounded-lg text-xs text-blue-800 space-y-1">
                  <div>
                    Cổng kiểm tra:{" "}
                    <strong>{qcModalStep.qualityGateName || qcModalStep.qualityGateCode}</strong>
                  </div>
                  <div>
                    Tiêu chuẩn: <strong>{qcModalStep.inspectionCriteria}</strong>
                  </div>
                  <div className="text-blue-700 font-semibold pt-0.5">
                    Quy tắc BR-008: Bắt buộc kiểm định đủ toàn bộ sản lượng lô (
                    {fmtNum(activeBatch.batchQty)} SP).
                  </div>
                </div>

                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="block text-xs font-bold text-slate-700 mb-1">
                      Tổng kiểm định
                    </label>
                    <input
                      type="number"
                      value={qcForm.inspectedQty}
                      readOnly
                      className="w-full px-3 py-2 border border-slate-300 bg-slate-100 text-slate-700 rounded-lg font-bold cursor-not-allowed"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-emerald-700 mb-1">
                      Số lượng ĐẠT
                    </label>
                    <input
                      type="number"
                      min={0}
                      max={qcForm.inspectedQty}
                      value={qcForm.passQty}
                      onChange={(e) => {
                        const v = Math.max(0, Math.min(qcForm.inspectedQty, Number(e.target.value)));
                        setQcForm((prev) => ({
                          ...prev,
                          passQty: v,
                          failQty: qcForm.inspectedQty - v,
                        }));
                      }}
                      className="w-full px-3 py-2 border border-emerald-300 bg-emerald-50 text-emerald-900 rounded-lg font-bold"
                      required
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-rose-700 mb-1">
                      Số lượng LỖI
                    </label>
                    <input
                      type="number"
                      min={0}
                      max={qcForm.inspectedQty}
                      value={qcForm.failQty}
                      onChange={(e) => {
                        const v = Math.max(0, Math.min(qcForm.inspectedQty, Number(e.target.value)));
                        setQcForm((prev) => ({
                          ...prev,
                          failQty: v,
                          passQty: qcForm.inspectedQty - v,
                        }));
                      }}
                      className="w-full px-3 py-2 border border-rose-300 bg-rose-50 text-rose-900 rounded-lg font-bold"
                      required
                    />
                  </div>
                </div>

                {qcForm.failQty > 0 && (
                  <div className="p-3.5 bg-rose-50 border border-rose-200 rounded-lg space-y-3">
                    <div className="text-xs font-bold text-rose-800">
                      Thông tin lỗi &amp; tách lô xử lý ({fmtNum(qcForm.failQty)} / {fmtNum(qcForm.inspectedQty)} sản phẩm lỗi):
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div>
                        <label className="block text-xs font-semibold text-slate-700 mb-1">
                          Mã lỗi vi phạm
                        </label>
                        <input
                          type="text"
                          placeholder="Ví dụ: ERR_CUT_01"
                          value={qcForm.defectCode || ""}
                          onChange={(e) =>
                            setQcForm((prev) => ({ ...prev, defectCode: e.target.value }))
                          }
                          className="w-full px-3 py-1.5 border border-slate-300 bg-white rounded-lg text-xs"
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-semibold text-slate-700 mb-1">
                          Phương án xử lý
                        </label>
                        <select
                          value={qcForm.disposition}
                          onChange={(e) =>
                            setQcForm((prev) => ({ ...prev, disposition: e.target.value }))
                          }
                          className="w-full px-3 py-1.5 border border-slate-300 bg-white rounded-lg text-xs font-semibold"
                        >
                          <option value="REWORK_SUB_CARD">
                            Tách lô phụ sửa chữa ({fmtNum(qcForm.failQty)} sản phẩm)
                          </option>
                          <option value="SCRAP_REPLENISH">
                            Hủy phế phẩm &amp; phát hành lô sản xuất bù ({fmtNum(qcForm.failQty)} sản phẩm)
                          </option>
                        </select>
                      </div>
                    </div>
                    <div>
                      <label className="block text-xs font-semibold text-slate-700 mb-1">
                        Tên / Mô tả chi tiết lỗi
                      </label>
                      <input
                        type="text"
                        placeholder="Ví dụ: Vát mộng không khít, nứt phôi thô, bọt khí sơn PU..."
                        value={qcForm.defectName || ""}
                        onChange={(e) =>
                          setQcForm((prev) => ({ ...prev, defectName: e.target.value }))
                        }
                        className="w-full px-3 py-1.5 border border-slate-300 bg-white rounded-lg text-xs"
                      />
                    </div>
                  </div>
                )}

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Ghi chú KCS</label>
                  <textarea
                    rows={2}
                    value={qcForm.notes || ""}
                    onChange={(e) => setQcForm((prev) => ({ ...prev, notes: e.target.value }))}
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs"
                    placeholder="Ghi chú chi tiết kết quả kiểm định..."
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
                  <button
                    type="button"
                    onClick={() => setQcModalStep(null)}
                    className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmittingAction}
                    className="px-5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50"
                  >
                    {isSubmittingAction ? "Đang lưu..." : "Xác nhận Nghiệm thu"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL 3: NGHIỆM THU LẠI LÔ PHỤ (REINSPECT SUB-BATCH) */}
        {reinspectModalStep && activeBatch && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <h3 className="text-base font-bold text-slate-900">
                  Nghiệm thu lại Lô phụ • {activeBatch.batchCode}
                </h3>
                <button
                  type="button"
                  onClick={() => setReinspectModalStep(null)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleSubmitReinspectSubBatch} className="space-y-4 text-sm">
                <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-xs text-amber-900">
                  Nghiệm thu lại toàn bộ sản lượng lô phụ sau sửa chữa/bù phôi tại công đoạn{" "}
                  <strong>
                    S-{reinspectModalStep.stageSequence} ({reinspectModalStep.stageName})
                  </strong>
                  .
                </div>

                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="block text-xs font-bold text-slate-700 mb-1">
                      Tổng kiểm định
                    </label>
                    <input
                      type="number"
                      value={reinspectForm.inspectedQty}
                      readOnly
                      className="w-full px-3 py-2 border border-slate-300 bg-slate-100 text-slate-700 rounded-lg font-bold cursor-not-allowed"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-emerald-700 mb-1">
                      Số lượng ĐẠT
                    </label>
                    <input
                      type="number"
                      min={0}
                      max={reinspectForm.inspectedQty}
                      value={reinspectForm.passQty}
                      onChange={(e) => {
                        const v = Math.max(
                          0,
                          Math.min(reinspectForm.inspectedQty, Number(e.target.value)),
                        );
                        setReinspectForm((prev) => ({
                          ...prev,
                          passQty: v,
                          failQty: reinspectForm.inspectedQty - v,
                        }));
                      }}
                      className="w-full px-3 py-2 border border-emerald-300 bg-emerald-50 text-emerald-900 rounded-lg font-bold"
                      required
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-rose-700 mb-1">
                      Không thể khắc phục
                    </label>
                    <input
                      type="number"
                      min={0}
                      max={reinspectForm.inspectedQty}
                      value={reinspectForm.failQty}
                      onChange={(e) => {
                        const v = Math.max(
                          0,
                          Math.min(reinspectForm.inspectedQty, Number(e.target.value)),
                        );
                        setReinspectForm((prev) => ({
                          ...prev,
                          failQty: v,
                          passQty: reinspectForm.inspectedQty - v,
                        }));
                      }}
                      className="w-full px-3 py-2 border border-rose-300 bg-rose-50 text-rose-900 rounded-lg font-bold"
                      required
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Ghi chú nghiệm thu lại
                  </label>
                  <textarea
                    rows={2}
                    maxLength={500}
                    value={reinspectForm.notes || ""}
                    onChange={(e) =>
                      setReinspectForm((prev) => ({ ...prev, notes: e.target.value }))
                    }
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs"
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
                  <button
                    type="button"
                    onClick={() => setReinspectModalStep(null)}
                    className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmittingAction}
                    className="px-5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50"
                  >
                    {isSubmittingAction ? "Đang lưu..." : "Xác nhận Nghiệm thu lại"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL 4: CHUYỂN CÔNG ĐOẠN LÔ CHÍNH */}
        {advanceModalStep && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <h3 className="text-base font-bold text-slate-900">Xác nhận Chuyển công đoạn</h3>
                <button
                  type="button"
                  onClick={() => setAdvanceModalStep(null)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleSubmitAdvanceStep} className="space-y-4 text-sm">
                <div className="p-3 bg-blue-50 rounded-lg text-xs text-blue-900 space-y-1">
                  <div>
                    Chuyển từ:{" "}
                    <strong>
                      S-{advanceModalStep.stageSequence} ({advanceModalStep.stageName})
                    </strong>
                  </div>
                  <div>
                    Đích đến:{" "}
                    <strong>
                      S-{(advanceModalStep.stageSequence + 1).toString().padStart(2, "0")}
                    </strong>
                  </div>
                  <div className="text-slate-600 pt-1">
                    Số lượng đã nghiệm thu đạt chuẩn:{" "}
                    <strong>{fmtNum(advanceModalStep.passedQty)} SP</strong>
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Ghi chú chuyển bước / Bàn giao
                  </label>
                  <textarea
                    rows={3}
                    maxLength={500}
                    value={advanceForm.notes || ""}
                    onChange={(e) => setAdvanceForm((prev) => ({ ...prev, notes: e.target.value }))}
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs"
                    placeholder="Ghi chú bàn giao phân xưởng..."
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
                  <button
                    type="button"
                    onClick={() => setAdvanceModalStep(null)}
                    className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmittingAction}
                    className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50"
                  >
                    {isSubmittingAction ? "Đang chuyển..." : "Xác nhận Chuyển công đoạn"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL 5: GỘP LÔ PHỤ VÀO LÔ CHÍNH (MERGE LOT) */}
        {mergeModalBatch && parentMainBatch && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <h3 className="text-base font-bold text-slate-900">
                  Tái hòa nhập / Gộp vào Lô chính
                </h3>
                <button
                  type="button"
                  onClick={() => setMergeModalBatch(null)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleSubmitMergeSubBatch} className="space-y-4 text-sm">
                <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-xs text-amber-900 space-y-1">
                  <div>
                    Thẻ phụ: <strong className="font-mono">{mergeModalBatch.batchCode}</strong> (
                    {fmtNum(mergeModalBatch.batchQty)} SP đạt chuẩn)
                  </div>
                  <div>
                    Gộp vào thẻ chính:{" "}
                    <strong className="font-mono">{parentMainBatch.batchCode}</strong> (Hiện tại:{" "}
                    {fmtNum(parentMainBatch.batchQty)} SP)
                  </div>
                  <div>
                    Sản lượng sau khi gộp:{" "}
                    <strong className="text-emerald-700">
                      {fmtNum(parentMainBatch.batchQty + mergeModalBatch.batchQty)} SP
                    </strong>{" "}
                    tại công đoạn S-{parentMainBatch.currentStageSeq}
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Lý do gộp lô</label>
                  <textarea
                    rows={2}
                    maxLength={500}
                    value={mergeReasonInput}
                    onChange={(e) => setMergeReasonInput(e.target.value)}
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs"
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
                  <button
                    type="button"
                    onClick={() => setMergeModalBatch(null)}
                    className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmittingAction}
                    className="px-5 py-2 bg-amber-600 hover:bg-amber-700 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50"
                  >
                    {isSubmittingAction ? "Đang gộp..." : "Xác nhận Gộp lô"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL 6: CHUYỂN TIẾP ĐỘC LẬP LÔ PHỤ */}
        {continueSubModalStep && activeBatch && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <h3 className="text-base font-bold text-slate-900">
                  Chuyển bước độc lập (Lô phụ)
                </h3>
                <button
                  type="button"
                  onClick={() => setContinueSubModalStep(null)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleSubmitContinueSubIndependently} className="space-y-4 text-sm">
                <div className="p-3 bg-blue-50 rounded-lg text-xs text-blue-900">
                  Thẻ nhánh phụ <strong className="font-mono">{activeBatch.batchCode}</strong> (
                  {fmtNum(activeBatch.batchQty)} SP) sẽ đi tiếp độc lập qua các công đoạn còn lại mà
                  không gộp vào lô chính.
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Ghi chú chuyển tiếp
                  </label>
                  <textarea
                    rows={2}
                    maxLength={500}
                    value={continueSubNotesInput}
                    onChange={(e) => setContinueSubNotesInput(e.target.value)}
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs"
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
                  <button
                    type="button"
                    onClick={() => setContinueSubModalStep(null)}
                    className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmittingAction}
                    className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50"
                  >
                    {isSubmittingAction ? "Đang chuyển..." : "Xác nhận Chuyển tiếp"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL 7: GỬI YÊU CẦU MỞ KHÓA / OVERDRIVE (TỔ TRƯỞNG) */}
        {requestOverdriveBatch && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <h3 className="text-base font-bold text-slate-900">
                  Gửi yêu cầu Chạy trước (Overdrive)
                </h3>
                <button
                  type="button"
                  onClick={() => setRequestOverdriveBatch(null)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleSubmitRequestOverdrive} className="space-y-4 text-sm">
                <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-xs text-amber-900">
                  Gửi đề xuất tới Chủ xưởng phê duyệt mở khóa cho lô{" "}
                  <strong className="font-mono">{requestOverdriveBatch.batchCode}</strong>.
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Chốt chặn Hold Point áp dụng (từ công đoạn 2 trở đi)
                  </label>
                  <input
                    type="number"
                    min={2}
                    max={Math.max(2, requestOverdriveBatch.steps.length)}
                    value={requestOverdriveForm.holdPointStageSeq ?? 5}
                    onChange={(e) =>
                      setRequestOverdriveForm((prev) => ({
                        ...prev,
                        holdPointStageSeq: Number(e.target.value),
                      }))
                    }
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg font-bold text-xs"
                    required
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Lý do giải trình kỹ thuật &amp; Rủi ro ETD (bắt buộc)
                  </label>
                  <textarea
                    rows={3}
                    maxLength={500}
                    value={requestOverdriveForm.justification}
                    onChange={(e) =>
                      setRequestOverdriveForm((prev) => ({
                        ...prev,
                        justification: e.target.value,
                      }))
                    }
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs"
                    required
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
                  <button
                    type="button"
                    onClick={() => setRequestOverdriveBatch(null)}
                    className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmittingAction}
                    className="px-5 py-2 bg-amber-600 hover:bg-amber-700 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50"
                  >
                    {isSubmittingAction ? "Đang gửi..." : "Gửi yêu cầu"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL 8: PHÊ DUYỆT / TỪ CHỐI OVERDRIVE (CHỦ XƯỞNG / ADMIN) */}
        {approveOverdriveBatch && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <h3 className="text-base font-bold text-slate-900">
                  Phê duyệt Mở khóa Overdrive • {approveOverdriveBatch.batchCode}
                </h3>
                <button
                  type="button"
                  onClick={() => setApproveOverdriveBatch(null)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleSubmitApproveOverdrive} className="space-y-4 text-sm">
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => setApproveOverdriveForm((p) => ({ ...p, approved: true }))}
                    className={`flex-1 py-2 text-xs font-bold rounded-lg border cursor-pointer ${
                      approveOverdriveForm.approved
                        ? "bg-emerald-600 text-white border-emerald-700"
                        : "bg-white text-slate-700 border-slate-300"
                    }`}
                  >
                    ✓ Chấp thuận mở khóa
                  </button>
                  <button
                    type="button"
                    onClick={() => setApproveOverdriveForm((p) => ({ ...p, approved: false }))}
                    className={`flex-1 py-2 text-xs font-bold rounded-lg border cursor-pointer ${
                      !approveOverdriveForm.approved
                        ? "bg-rose-600 text-white border-rose-700"
                        : "bg-white text-slate-700 border-slate-300"
                    }`}
                  >
                    ✕ Từ chối (Giữ khóa)
                  </button>
                </div>

                {approveOverdriveForm.approved && (
                  <div>
                    <label className="block text-xs font-bold text-slate-700 mb-1">
                      Công đoạn chốt chặn Hold Point bắt buộc
                    </label>
                    <input
                      type="number"
                      min={2}
                      max={Math.max(2, approveOverdriveBatch.steps.length)}
                      value={approveOverdriveForm.holdPointStageSeq ?? 5}
                      onChange={(e) =>
                        setApproveOverdriveForm((prev) => ({
                          ...prev,
                          holdPointStageSeq: Number(e.target.value),
                        }))
                      }
                      className="w-full px-3 py-2 border border-slate-300 rounded-lg font-bold text-xs"
                      required
                    />
                  </div>
                )}

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    {approveOverdriveForm.approved
                      ? "Ghi chú chỉ đạo của Chủ xưởng"
                      : "Lý do từ chối (tối thiểu 10 ký tự)"}
                  </label>
                  <textarea
                    rows={3}
                    maxLength={500}
                    value={approveOverdriveForm.notes || ""}
                    onChange={(e) =>
                      setApproveOverdriveForm((prev) => ({ ...prev, notes: e.target.value }))
                    }
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs"
                    required={!approveOverdriveForm.approved}
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
                  <button
                    type="button"
                    onClick={() => setApproveOverdriveBatch(null)}
                    className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmittingAction}
                    className={`px-5 py-2 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50 ${
                      approveOverdriveForm.approved
                        ? "bg-emerald-600 hover:bg-emerald-700"
                        : "bg-rose-600 hover:bg-rose-700"
                    }`}
                  >
                    {isSubmittingAction ? "Đang xử lý..." : "Xác nhận quyết định"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL 9: HOÀN TẤT & NHẬP KHO THÀNH PHẨM */}
        {completeBatchModal && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <h3 className="text-base font-bold text-slate-900">
                  Hoàn tất Lô &amp; Nhập kho thành phẩm
                </h3>
                <button
                  type="button"
                  onClick={() => setCompleteBatchModal(null)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleSubmitCompleteBatch} className="space-y-4 text-sm">
                <div className="p-3 bg-purple-50 rounded-lg text-xs text-purple-900 space-y-1">
                  <div>
                    Mã lô hoàn tất: <strong className="font-mono">{completeBatchModal.batchCode}</strong>
                  </div>
                  <div>
                    Sản phẩm: <strong>{currentLineItem?.productName}</strong>
                  </div>
                  <div>
                    Số lượng định mức lô: <strong>{fmtNum(completeBatchModal.batchQty)} SP</strong>
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Số lượng nhập kho thực tế
                  </label>
                  <input
                    type="number"
                    min={1}
                    max={completeBatchModal.batchQty}
                    value={completeForm.receivedQty ?? completeBatchModal.batchQty}
                    onChange={(e) =>
                      setCompleteForm((prev) => ({ ...prev, receivedQty: Number(e.target.value) }))
                    }
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg font-bold"
                    required
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Vị trí kho lưu trữ
                  </label>
                  <input
                    type="text"
                    value={completeForm.warehouseLocation || ""}
                    onChange={(e) =>
                      setCompleteForm((prev) => ({ ...prev, warehouseLocation: e.target.value }))
                    }
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs"
                    placeholder="Ví dụ: KHO-TP-A1"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Ghi chú bàn giao
                  </label>
                  <textarea
                    rows={2}
                    value={completeForm.handoverNote || ""}
                    onChange={(e) =>
                      setCompleteForm((prev) => ({ ...prev, handoverNote: e.target.value }))
                    }
                    className="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs"
                    placeholder="Ghi chú bàn giao thủ kho..."
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-200">
                  <button
                    type="button"
                    onClick={() => setCompleteBatchModal(null)}
                    className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmittingAction}
                    className="px-5 py-2 bg-purple-600 hover:bg-purple-700 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50"
                  >
                    {isSubmittingAction ? "Đang nhập kho..." : "Xác nhận Nhập kho thành phẩm"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL 10: XEM CHI TIẾT CÔNG ĐOẠN (KÈM NGÀY BẮT ĐẦU & NGÀY KẾT THÚC) */}
        {detailModalContext && (
          <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
              <div className="flex items-center justify-between border-b border-slate-200 pb-3">
                <h3 className="text-base font-bold text-slate-900">
                  Chi tiết Công đoạn S-{detailModalContext.step.stageSequence}:{" "}
                  {detailModalContext.step.stageName}
                </h3>
                <button
                  type="button"
                  onClick={() => setDetailModalContext(null)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              </div>

              <div className="space-y-3 text-xs">
                <div className="p-3 bg-slate-50 rounded-lg space-y-1">
                  <div>
                    Trạm máy:{" "}
                    <strong>
                      {detailModalContext.step.workstationName ||
                        detailModalContext.step.workstationCode}
                    </strong>{" "}
                    (<span className="font-mono">{detailModalContext.step.workstationCode}</span>)
                  </div>
                  <div>
                    Cổng chất lượng:{" "}
                    <strong>
                      {detailModalContext.step.qualityGateName ||
                        detailModalContext.step.qualityGateCode}
                    </strong>
                  </div>
                  <div>
                    Chốt chặn kỹ thuật:{" "}
                    <strong>
                      {detailModalContext.step.isHoldPoint ? "CÓ (Hold Point)" : "Không"}
                    </strong>
                  </div>
                </div>

                {/* Ngày bắt đầu & Ngày kết thúc */}
                <div className="grid grid-cols-2 gap-2">
                  <div className="p-2.5 bg-blue-50/70 border border-blue-200 rounded-lg">
                    <span className="text-slate-500">Ngày bắt đầu</span>
                    <div className="font-mono font-bold text-slate-900 mt-0.5">
                      {detailModalContext.startedAt
                        ? fmtDateTime(detailModalContext.startedAt)
                        : "Chưa bắt đầu"}
                    </div>
                  </div>
                  <div className="p-2.5 bg-emerald-50/70 border border-emerald-200 rounded-lg">
                    <span className="text-slate-500">Ngày kết thúc</span>
                    <div className="font-mono font-bold text-emerald-800 mt-0.5">
                      {detailModalContext.completedAt
                        ? fmtDateTime(detailModalContext.completedAt)
                        : detailModalContext.startedAt
                          ? "Đang thực hiện"
                          : "—"}
                    </div>
                  </div>
                </div>

                <div className="space-y-1">
                  <span className="font-bold text-slate-700">Tiêu chuẩn kiểm tra:</span>
                  <p className="p-2.5 bg-slate-100 rounded-lg text-slate-800 font-mono text-[11px]">
                    {detailModalContext.step.inspectionCriteria || "Chưa có văn bản tiêu chuẩn"}
                  </p>
                </div>

                <div className="grid grid-cols-3 gap-2 text-center pt-1">
                  <div className="p-2 bg-slate-50 border border-slate-200 rounded-lg">
                    <span className="text-slate-500">Định mức</span>
                    <div className="text-sm font-bold text-slate-800">
                      {fmtNum(detailModalContext.step.targetQty)}
                    </div>
                  </div>
                  <div className="p-2 bg-emerald-50 border border-emerald-200 rounded-lg">
                    <span className="text-emerald-700">Đã đạt</span>
                    <div className="text-sm font-bold text-emerald-800">
                      {fmtNum(detailModalContext.step.passedQty)}
                    </div>
                  </div>
                  <div className="p-2 bg-rose-50 border border-rose-200 rounded-lg">
                    <span className="text-rose-700">Phát hiện lỗi</span>
                    <div className="text-sm font-bold text-rose-800">
                      {fmtNum(detailModalContext.step.defectQty)}
                    </div>
                  </div>
                </div>
              </div>

              <div className="flex justify-end pt-3 border-t border-slate-200">
                <button
                  type="button"
                  onClick={() => setDetailModalContext(null)}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-lg text-xs cursor-pointer"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </WmmsAppShell>
  );
}
