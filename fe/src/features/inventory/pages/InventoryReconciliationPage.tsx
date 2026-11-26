import { Fragment, useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ApiError } from "../../../lib/api/apiError";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useWmms } from "../../auth/hooks/WmmsContext";
import { useSession } from "../../auth/store/SessionContext";
import { orderIntakeApi } from "../../../services/wmmsApi";
import { mrpApi } from "../api/mrpApi";
import type {
  AutoAllocationResultDto,
  MaterialOrderAllocationDto,
  MrpAnalysisResultDto,
  MrpMaterialRequirementLineDto,
} from "../types";

const fmtNum = (n?: number | null) => (n ?? 0).toLocaleString("vi-VN");
const fmtDate = (v?: string | null) => {
  if (!v) return "—";
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleDateString("vi-VN");
};

// Thứ tự ưu tiên nhóm trạng thái: Thiếu hết (1) -> Thiếu 1 phần (2) -> Đủ tồn kho (3) -> Đã giữ chỗ (4)
const getSupplyRank = (status?: string) => {
  if (status === "CRITICAL_SHORTAGE") return 1;
  if (status === "PARTIAL_SHORTAGE") return 2;
  if (status === "SUFFICIENT") return 3;
  return 4; // RESERVED
};

const SUPPLY_BADGE_CLASS: Record<string, string> = {
  RESERVED: "bg-emerald-50 text-emerald-700 border-emerald-200",
  SUFFICIENT: "bg-blue-50 text-blue-700 border-blue-200",
  PARTIAL_SHORTAGE: "bg-amber-50 text-amber-800 border-amber-200",
  CRITICAL_SHORTAGE: "bg-rose-50 text-rose-700 border-rose-200",
};

export default function InventoryReconciliationPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { currentSpaceKey, setCurrentSpaceKey } = useWmms();
  const { currentUser } = useSession();

  const activeRole = currentUser?.activeRole || "OWNER";
  const canAccessBomPage = ["ADMIN", "OWNER", "PLANNER"].includes(activeRole);
  const canAccessProductionPage = [
    "ADMIN",
    "OWNER",
    "PLANNER",
    "LEADER",
    "QC",
    "WAREHOUSE",
  ].includes(activeRole);

  const spaceParam = searchParams.get("spaceKey");
  const activeSpaceKey = spaceParam || currentSpaceKey || "";

  const [spaceInfo, setSpaceInfo] = useState<{
    spaceId?: number;
    orderId?: number;
    spaceKey: string;
    poNumber: string;
    customerName: string;
    etdDate?: string;
    totalUnits?: number;
    lineItemsCount?: number;
  } | null>(null);

  const [isLoadingSpace, setIsLoadingSpace] = useState<boolean>(true);
  const [spaceError, setSpaceError] = useState<string | null>(null);

  // MRP Analysis Data
  const [mrpData, setMrpData] = useState<MrpAnalysisResultDto | null>(null);
  const [isLoadingMrp, setIsLoadingMrp] = useState<boolean>(false);
  const [mrpError, setMrpError] = useState<string | null>(null);
  const [toastBanner, setToastBanner] = useState<{
    type: "success" | "error" | "info";
    text: string;
  } | null>(null);

  // Bộ lọc bảng vật tư
  const [searchKeyword, setSearchKeyword] = useState<string>("");
  const [filterSupplyStatus, setFilterSupplyStatus] = useState<string>("ALL");
  const [expandedMaterialId, setExpandedMaterialId] = useState<number | null>(null);

  // Phân trang danh sách vật tư (Client-side paging)
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);

  // Thực thi Phân bổ tự động
  const [autoQueuePr, setAutoQueuePr] = useState<boolean>(true);
  const [isAllocating, setIsAllocating] = useState<boolean>(false);
  const [allocationResult, setAllocationResult] = useState<AutoAllocationResultDto | null>(null);

  // Tái phân bổ ETD
  const [isReallocatingEtd, setIsReallocatingEtd] = useState<boolean>(false);

  // Hủy giữ chỗ (Release Allocation)
  const [isReleasing, setIsReleasing] = useState<boolean>(false);
  // Danh sách phân bổ giữ chỗ thực tế của Order Space
  const [spaceAllocations, setSpaceAllocations] = useState<MaterialOrderAllocationDto[]>([]);

  useEffect(() => {
    if (spaceParam && spaceParam !== currentSpaceKey) {
      setCurrentSpaceKey(spaceParam);
    }
  }, [spaceParam, currentSpaceKey, setCurrentSpaceKey]);

  // Load Order Space data & analyze MRP for this Space
  const loadOrderSpaceData = async (spaceKey: string) => {
    if (!spaceKey) {
      setIsLoadingSpace(false);
      setSpaceError(
        "Không xác định được Không gian đơn hàng (Order Space). Vui lòng truy cập từ Danh sách đơn hàng.",
      );
      setSpaceInfo(null);
      setMrpData(null);
      setSpaceAllocations([]);
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
        setSpaceInfo({
          spaceId,
          orderId,
          spaceKey: detail.spaceKey || spaceKey,
          poNumber: detail.poNumber || "",
          customerName: detail.customerName || "",
          etdDate: detail.etdDate,
          totalUnits: detail.totalOrderedUnits,
          lineItemsCount: Array.isArray(detail.lineItems) ? detail.lineItems.length : 0,
        });

        // Phân tích MRP & lấy danh sách phân bổ giữ chỗ thực tế cho Space này
        setIsLoadingMrp(true);
        setMrpError(null);
        try {
          const [mrp, allocs] = await Promise.all([
            spaceId
              ? mrpApi.analyzeOrderSpaceMrp(spaceId)
              : orderId > 0
                ? mrpApi.analyzeOrderMrp(orderId)
                : mrpApi.analyzeFactoryMrp(),
            spaceId
              ? mrpApi
                  .getOrderSpaceAllocations(spaceId)
                  .catch(() => [] as MaterialOrderAllocationDto[])
              : Promise.resolve([] as MaterialOrderAllocationDto[]),
          ]);
          setMrpData(mrp);
          setSpaceAllocations(Array.isArray(allocs) ? allocs : []);
        } catch (mrpErr) {
          let msg = `Không thể phân tích nhu cầu vật tư MRP cho Không gian "${spaceKey}".`;
          if (mrpErr instanceof ApiError) {
            msg = mrpErr.message || msg;
          } else if (mrpErr instanceof Error) {
            msg = mrpErr.message || msg;
          }
          setMrpError(msg);
          setSpaceAllocations([]);
        } finally {
          setIsLoadingMrp(false);
        }
      } else {
        setSpaceError(`Lỗi: Không tìm thấy thông tin Không gian đơn hàng "${spaceKey}".`);
        setSpaceAllocations([]);
      }
    } catch (err: any) {
      setSpaceError(
        err?.response?.data?.message ||
        err?.message ||
        `Không thể tải thông tin Không gian đơn hàng "${spaceKey}" từ máy chủ.`
      );
      setSpaceAllocations([]);
    } finally {
      setIsLoadingSpace(false);
    }
  };

  useEffect(() => {
    loadOrderSpaceData(activeSpaceKey);
  }, [activeSpaceKey]);

  // Thực thi Khóa phân bổ mềm (Soft-Allocation) & Gom PR ngày cho Space này
  const handleExecuteAutoAllocation = async () => {
    if (!spaceInfo) return;
    setIsAllocating(true);
    setToastBanner(null);
    try {
      let res: AutoAllocationResultDto;
      if (spaceInfo.spaceId) {
        res = await mrpApi.autoAllocateOrderSpace(spaceInfo.spaceId, {
          autoQueueShortageToDailyPr: autoQueuePr,
          notes: `Thực thi phân bổ tự động MRP cho Không gian đơn hàng ${spaceInfo.spaceKey}.`,
        });
      } else if (spaceInfo.orderId && spaceInfo.orderId > 0) {
        res = await mrpApi.autoAllocateOrder(spaceInfo.orderId, {
          autoQueueShortageToDailyPr: autoQueuePr,
          notes: `Thực thi phân bổ tự động MRP cho đơn hàng ${spaceInfo.poNumber}.`,
        });
      } else {
        throw new Error("Không xác định được ID đơn hàng để phân bổ.");
      }

      setAllocationResult(res);
      setToastBanner({
        type: "success",
        text: `Thực thi phân bổ thành công! Đã cập nhật ${res.totalMaterialAllocationsUpdated} vị trí giữ chỗ cho ${spaceInfo.spaceKey}. ${
          res.fullyAllocatedItemsCount > 0
            ? `${res.fullyAllocatedItemsCount} sản phẩm đã sẵn sàng sản xuất.`
            : ""
        }`,
      });

      // Tải lại phân tích MRP sau khi phân bổ
      loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể thực thi phân bổ tồn kho tự động.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (err instanceof Error) {
        msg = err.message;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsAllocating(false);
    }
  };

  // Tái phân bổ theo ETD sớm nhất (FIFO by ETD)
  const handleReallocateByEtd = async () => {
    setIsReallocatingEtd(true);
    setToastBanner(null);
    try {
      const res = await mrpApi.reallocateByEtd({
        referenceCode: `REALLOC-${Date.now()}`,
      });
      setToastBanner({
        type: "success",
        text: `Tái phân bổ theo ETD thành công! Đã tăng giữ chỗ cho ${res.allocationsIncreasedCount} vị trí, mở khóa ${res.orderItemsUnlockedToReadyCount} sản phẩm sang SẴN SÀNG SX.`,
      });
      loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể tái phân bổ tồn kho theo ETD.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (err instanceof Error) {
        msg = err.message || msg;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsReallocatingEtd(false);
    }
  };

  // Trạng thái giữ chỗ và kiểm tra điều kiện chặn hủy của Space
  const spaceAllocationSummary = useMemo(() => {
    const list = mrpData?.materialRequirements || [];
    const totalMrpReserved = list.reduce(
      (sum, m) => sum + (Number(m.alreadyReservedForTargetQty) || 0),
      0,
    );
    const totalAllocatableNow = list.reduce(
      (sum, m) => sum + (Number(m.allocatableNowQty) || 0),
      0,
    );

    // Tính lượng giữ chỗ trực tiếp từ bảng phân bổ (material_allocations) của Space
    const totalDirectReserved = spaceAllocations.reduce((sum, a) => {
      if (a.allocationStatus === "RESERVED" || (Number(a.reservedQty) || 0) > 0) {
        return sum + (Number(a.reservedQty) || 0);
      }
      return sum;
    }, 0);

    const totalReserved = Math.max(totalMrpReserved, totalDirectReserved);

    let isBlocked = false;
    let blockReason = "";

    // 1. Kiểm tra điều kiện chặn từ BOM itemBreakdowns
    for (const m of list) {
      for (const bd of m.itemBreakdowns || []) {
        if ((bd.issuedQty || 0) > 0) {
          isBlocked = true;
          blockReason =
            "Không thể hủy giữ chỗ vì vật tư đã được xuất kho cấp phát thực tế (Issued).";
          break;
        }
        if (bd.orderItemStatus === "IN_PROD" || bd.orderItemStatus === "DONE") {
          isBlocked = true;
          blockReason =
            "Không thể hủy giữ chỗ vì sản phẩm trong đơn đã chuyển sang sản xuất (In Production / Done).";
          break;
        }
      }
      if (isBlocked) break;
    }

    // 2. Kiểm tra điều kiện chặn từ spaceAllocations trực tiếp
    if (!isBlocked && spaceAllocations.length > 0) {
      for (const a of spaceAllocations) {
        if ((Number(a.issuedQty) || 0) > 0) {
          isBlocked = true;
          blockReason =
            "Không thể hủy giữ chỗ vì vật tư đã được xuất kho cấp phát thực tế (Issued).";
          break;
        }
        if (a.allocationStatus === "IN_PROD" || a.allocationStatus === "DONE") {
          isBlocked = true;
          blockReason =
            "Không thể hủy giữ chỗ vì sản phẩm trong đơn đã chuyển sang sản xuất (In Production / Done).";
          break;
        }
      }
    }

    const hasAnyAllocation = totalReserved > 0 || spaceAllocations.length > 0;
    const isFullyReserved =
      hasAnyAllocation && (list.length === 0 || totalAllocatableNow === 0);

    return {
      totalReserved,
      totalAllocatableNow,
      hasAnyAllocation,
      isFullyReserved,
      isBlocked,
      blockReason,
    };
  }, [mrpData?.materialRequirements, spaceAllocations]);

  // Hủy giữ chỗ toàn bộ vật tư của Không gian đơn hàng (Release Allocations)
  const handleReleaseAllocations = async () => {
    const targetOrderId =
      spaceInfo?.orderId || mrpData?.orderId || spaceAllocations[0]?.orderId;
    if (!targetOrderId) {
      setToastBanner({
        type: "error",
        text: "Không xác định được ID đơn hàng để hủy giữ chỗ vật tư.",
      });
      return;
    }

    if (spaceAllocationSummary.isBlocked) {
      setToastBanner({
        type: "error",
        text: spaceAllocationSummary.blockReason,
      });
      return;
    }

    const confirmRelease = window.confirm(
      `Bạn có chắc chắn muốn HỦY GIỮ CHỖ toàn bộ vật tư cho không gian "${activeSpaceKey}"?\nToàn bộ vật tư đã giữ sẽ được hoàn trả về Tồn kho khả dụng (Available Stock) để các đơn hàng khác có thể sử dụng.`,
    );
    if (!confirmRelease) return;

    setIsReleasing(true);
    setToastBanner(null);
    try {
      await mrpApi.releaseOrderAllocations(
        targetOrderId,
        `Người dùng hủy giữ chỗ vật tư cho không gian ${activeSpaceKey} trên giao diện đối chiếu tồn kho.`,
      );
      setAllocationResult(null);
      setSpaceAllocations([]);
      setToastBanner({
        type: "info",
        text: `Đã hủy giữ chỗ thành công! Toàn bộ vật tư của "${activeSpaceKey}" đã được hoàn trả về Tồn kho khả dụng của xưởng.`,
      });
      // Tải lại phân tích MRP sau khi hủy giữ chỗ
      loadOrderSpaceData(activeSpaceKey);
    } catch (err) {
      let msg = "Không thể hủy giữ chỗ vật tư cho đơn hàng.";
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (err instanceof Error) {
        msg = err.message;
      }
      setToastBanner({ type: "error", text: msg });
    } finally {
      setIsReleasing(false);
    }
  };

  // Lọc & Sắp xếp danh sách vật tư của Space: ĐẨY VẬT TƯ THIẾU HỤT LÊN ĐẦU
  const sortedFilteredMaterials = useMemo(() => {
    const list = (mrpData?.materialRequirements || []).filter((item) => {
      if (filterSupplyStatus === "INSUFFICIENT") {
        if (
          item.supplyStatus !== "PARTIAL_SHORTAGE" &&
          item.supplyStatus !== "CRITICAL_SHORTAGE"
        ) {
          return false;
        }
      } else if (
        filterSupplyStatus !== "ALL" &&
        item.supplyStatus !== filterSupplyStatus
      ) {
        return false;
      }
      if (searchKeyword.trim()) {
        const kw = searchKeyword.trim().toLowerCase();
        const match =
          item.materialCode.toLowerCase().includes(kw) ||
          item.materialName.toLowerCase().includes(kw) ||
          item.materialGroup.toLowerCase().includes(kw) ||
          (item.dimensionSpec || "").toLowerCase().includes(kw);
        if (!match) return false;
      }
      return true;
    });

    // Sắp xếp ưu tiên:
    // 1. Nhóm thiếu hụt (CRITICAL_SHORTAGE -> PARTIAL_SHORTAGE -> SUFFICIENT)
    // 2. Lượng thiếu hụt ròng (netShortageQty) giảm dần
    // 3. Mã vật tư tăng dần
    return list.sort((a, b) => {
      const rankA = getSupplyRank(a.supplyStatus);
      const rankB = getSupplyRank(b.supplyStatus);
      if (rankA !== rankB) return rankA - rankB;

      if (b.netShortageQty !== a.netShortageQty) {
        return b.netShortageQty - a.netShortageQty;
      }

      return a.materialCode.localeCompare(b.materialCode);
    });
  }, [mrpData?.materialRequirements, filterSupplyStatus, searchKeyword]);

  // Cắt mảng theo phân trang (Paging)
  const totalItems = sortedFilteredMaterials.length;
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  const startItem = totalItems === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endItem = Math.min(pageNumber * pageSize, totalItems);

  const pagedMaterials = useMemo(() => {
    const start = (pageNumber - 1) * pageSize;
    return sortedFilteredMaterials.slice(start, start + pageSize);
  }, [sortedFilteredMaterials, pageNumber, pageSize]);

  return (
    <>
      <HeaderTopLeft>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() =>
              navigate(
                activeSpaceKey
                  ? `/orders/spaces/${encodeURIComponent(activeSpaceKey)}`
                  : "/orders",
              )
            }
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
            <span>{activeSpaceKey ? "Danh sách sản phẩm" : "Danh sách đơn hàng"}</span>
          </button>

        </div>
      </HeaderTopLeft>
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

        {/* Modal / Banner Kết quả sau khi phân bổ & Gom PR */}
        {allocationResult && (
          <div className="p-5 bg-gradient-to-r from-emerald-50 to-blue-50 border border-emerald-200 rounded-xl shadow-xs space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-2.5">
                <div className="w-8 h-8 rounded-full bg-emerald-600 text-white flex items-center justify-center font-bold text-base">
                  ✓
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900">
                    Phân bổ tồn kho tự động &amp; Khóa giữ chỗ mềm hoàn tất
                  </h3>
                  <p className="text-xs text-slate-600 mt-0.5">
                    Đã cập nhật{" "}
                    <strong>{allocationResult.totalMaterialAllocationsUpdated}</strong> bản ghi
                    giữ chỗ cho Không gian đơn hàng {allocationResult.spaceKey || activeSpaceKey}.
                  </p>
                </div>
              </div>

              {/* Nút nối thẳng sang Bước 2: Xem & Duyệt PR ngày */}
              {canAccessPrPage && allocationResult.consolidatedPrId && (
                <button
                  type="button"
                  onClick={() =>
                    navigate(
                      `/purchasing/requisitions?prId=${allocationResult.consolidatedPrId}`,
                    )
                  }
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold rounded-lg shadow-sm transition flex items-center gap-2 cursor-pointer"
                >
                  <span>
                    Xem &amp; Duyệt Phiếu PR{" "}
                    {allocationResult.consolidatedPrCode} →
                  </span>
                </button>
              )}
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs pt-1">
              <div className="p-2.5 bg-white rounded-lg border border-slate-200">
                <span className="text-slate-500">Sản phẩm đủ vật tư:</span>
                <div className="text-sm font-bold text-emerald-700 mt-0.5">
                  {allocationResult.fullyAllocatedItemsCount} sản phẩm (SẴN SÀNG SX)
                </div>
              </div>
              <div className="p-2.5 bg-white rounded-lg border border-slate-200">
                <span className="text-slate-500">Sản phẩm còn thiếu vật tư:</span>
                <div className="text-sm font-bold text-amber-700 mt-0.5">
                  {allocationResult.waitingMaterialItemsCount} sản phẩm (CHỜ VẬT TƯ)
                </div>
              </div>
              <div className="p-2.5 bg-white rounded-lg border border-slate-200">
                <span className="text-slate-500">Gom vào PR tổng hợp:</span>
                <div className="text-sm font-bold text-blue-700 mt-0.5">
                  {allocationResult.consolidatedPrCode
                    ? `${allocationResult.queuedShortageMaterialsCount} vật tư (${allocationResult.consolidatedPrCode})`
                    : "Không phát sinh thiếu"}
                </div>
              </div>
              <div className="p-2.5 bg-white rounded-lg border border-slate-200">
                <span className="text-slate-500">Trạng thái đơn hàng:</span>
                <div className="text-sm font-bold text-slate-800 mt-0.5">
                  {allocationResult.overallReadinessStatus === "READY_TO_PRODUCE"
                    ? "ĐỦ VẬT TƯ SẢN XUẤT"
                    : "ĐANG CHỜ MUA BỔ SUNG"}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Thông báo lỗi nếu không tìm thấy Không gian đơn hàng */}
        {!isLoadingSpace && (spaceError || !activeSpaceKey) && (
          <div className="px-5 py-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm font-semibold flex items-center justify-between gap-4 shadow-xs">
            <span>
              {spaceError ||
                "Không xác định được Không gian đơn hàng. Vui lòng quay lại Danh sách đơn hàng để chọn."}
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

        {/* Header Thông tin Không gian đơn hàng (Order Space Context) */}
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
                  • Khách hàng: <strong className="text-slate-900">{spaceInfo.customerName}</strong>
                </span>
              )}
            </div>

            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight mt-2">
              Đối chiếu Tồn kho &amp; Phân bổ vật tư đơn hàng
            </h1>
            <p className="text-sm text-slate-600 mt-1">
              Tính toán nhu cầu nguyên vật liệu từ cấu trúc BOM của các sản phẩm thuộc Không gian{" "}
              <strong>{activeSpaceKey}</strong>, đối chiếu với tồn kho khả dụng và khóa giữ chỗ mềm.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3 shrink-0">
            <div className="px-4 py-2.5 rounded-lg bg-slate-50 border border-slate-200 text-sm text-slate-700">
              Sản phẩm trong Space:{" "}
              <strong className="font-extrabold text-slate-900">
                {spaceInfo?.lineItemsCount || mrpData?.totalOrderItemsCount || 0}
              </strong>
            </div>

            {canAccessPrPage && (
              <button
                type="button"
                onClick={() => navigate("/purchasing/requisitions")}
                className="px-3.5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-lg transition border border-slate-300 flex items-center gap-1.5 cursor-pointer whitespace-nowrap"
              >
                <span>Yêu cầu mua hàng →</span>
              </button>
            )}
          </div>
        </div>

        {/* 4 Thẻ KPI Tóm tắt Kết quả MRP của Space */}
        {mrpData && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {/* Thẻ 1: Trạng thái tổng thể */}
            <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                TRẠNG THÁI SẴN SÀNG
              </div>
              {mrpData.shortageMaterialsCount > 0 ? (
                <>
                  <div className="text-xl font-extrabold mt-1.5 flex items-center gap-2 text-rose-600">
                    <span className="w-3 h-3 rounded-full bg-rose-500 animate-pulse" />
                    <span>THIẾU VẬT TƯ (WAITING)</span>
                  </div>
                  <div className="text-xs text-slate-500 mt-1">
                    Có {mrpData.shortageMaterialsCount} mã vật tư bị thiếu hụt, cần gom PR đặt mua
                  </div>
                </>
              ) : (mrpData.totalDistinctMaterialsCount || 0) === 0 ? (
                spaceAllocationSummary.hasAnyAllocation ? (
                  <>
                    <div className="text-xl font-extrabold mt-1.5 flex items-center gap-2 text-emerald-600">
                      <span className="w-3 h-3 rounded-full bg-emerald-500" />
                      <span>ĐÃ KHÓA GIỮ CHỖ</span>
                    </div>
                    <div className="text-xs text-slate-500 mt-1">
                      Đang giữ {fmtNum(spaceAllocationSummary.totalReserved)} đơn vị vật tư trong hệ thống
                    </div>
                  </>
                ) : (
                  <>
                    <div className="text-xl font-extrabold mt-1.5 flex items-center gap-2 text-slate-500">
                      <span className="w-3 h-3 rounded-full bg-slate-400" />
                      <span>CHƯA THIẾT LẬP BOM</span>
                    </div>
                    <div className="text-xs text-slate-500 mt-1">
                      Chưa có sản phẩm nào được thiết lập định mức vật tư trong BOM
                    </div>
                  </>
                )
              ) : spaceAllocationSummary.hasAnyAllocation &&
                spaceAllocationSummary.totalAllocatableNow === 0 ? (
                <>
                  <div className="text-xl font-extrabold mt-1.5 flex items-center gap-2 text-emerald-600">
                    <span className="w-3 h-3 rounded-full bg-emerald-500" />
                    <span>ĐÃ KHÓA ĐỦ VẬT TƯ (LOCKED)</span>
                  </div>
                  <div className="text-xs text-slate-500 mt-1">
                    Toàn bộ vật tư định mức trong BOM đã được khóa giữ chỗ
                  </div>
                </>
              ) : (
                <>
                  <div className="text-xl font-extrabold mt-1.5 flex items-center gap-2 text-blue-600">
                    <span className="w-3 h-3 rounded-full bg-blue-500" />
                    <span>ĐỦ TỒN KHO (CHỜ KHÓA)</span>
                  </div>
                  <div className="text-xs text-slate-500 mt-1">
                    Tồn khả dụng đáp ứng đủ định mức BOM, bấm Thực thi để khóa giữ chỗ
                  </div>
                </>
              )}
            </div>

            {/* Thẻ 2: Dòng sản phẩm */}
            <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                SẢN PHẨM TRONG ĐƠN
              </div>
              <div className="text-2xl font-extrabold text-slate-900 mt-1.5">
                <span className="text-emerald-600">{mrpData.readyOrderItemsCount}</span> /{" "}
                {mrpData.totalOrderItemsCount} sản phẩm
              </div>
              <div className="text-xs text-slate-500 mt-1">
                {mrpData.shortageMaterialsCount === 0 ? (
                  mrpData.readyOrderItemsCount < mrpData.totalOrderItemsCount ? (
                    <span className="text-slate-600 font-medium">
                      <strong className="text-emerald-700">
                        {mrpData.readyOrderItemsCount} SP
                      </strong>{" "}
                      đã đủ vật tư •{" "}
                      <strong className="text-amber-700">
                        {mrpData.totalOrderItemsCount - mrpData.readyOrderItemsCount} SP
                      </strong>{" "}
                      chưa thiết lập BOM
                    </span>
                  ) : (
                    <span className="text-emerald-700 font-medium">
                      Tất cả {mrpData.totalOrderItemsCount} sản phẩm đã sẵn sàng sản xuất
                    </span>
                  )
                ) : (
                  <span className="text-amber-600 font-semibold">
                    {mrpData.waitingMaterialItemsCount} sản phẩm đang chờ vật tư
                  </span>
                )}
              </div>
            </div>

            {/* Thẻ 3: Chủng loại vật tư */}
            <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                CHỦNG LOẠI VẬT TƯ CẦN DÙNG
              </div>
              <div className="text-2xl font-extrabold text-slate-900 mt-1.5">
                <span className="text-emerald-600">{mrpData.sufficientMaterialsCount} đủ</span>
                {mrpData.shortageMaterialsCount > 0 && (
                  <span className="text-rose-600 text-lg font-bold ml-2">
                    ({mrpData.shortageMaterialsCount} thiếu)
                  </span>
                )}
              </div>
              <div className="text-xs text-slate-500 mt-1">
                Tổng {mrpData.totalDistinctMaterialsCount} mã vật tư được định mức trong BOM
              </div>
            </div>

            {/* Thẻ 4: Hạn giao hàng (ETD) */}
            <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
              <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                HẠN GIAO HÀNG (ETD)
              </div>
              <div
                className={`text-xl font-extrabold mt-1.5 ${
                  mrpData.isEtdAtRisk ? "text-rose-600" : "text-slate-900"
                }`}
              >
                {fmtDate(mrpData.etdDate || spaceInfo?.etdDate)}
              </div>
              <div className="text-xs text-slate-500 mt-1">
                {mrpData.isEtdAtRisk ? (
                  <span className="text-rose-600 font-bold flex items-center gap-1">
                    ⚠ Cảnh báo: Rủi ro trễ hạn ETD do thiếu vật tư!
                  </span>
                ) : (
                  "Tiến độ vật tư đảm bảo hạn giao hàng"
                )}
              </div>
            </div>
          </div>
        )}

        {/* Thanh Công cụ Thực thi Đối chiếu & Phân bổ tự động */}
        {canExecuteAllocation && (
          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <label className="flex items-center gap-2 cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={autoQueuePr}
                  onChange={(e) => setAutoQueuePr(e.target.checked)}
                  className="w-4 h-4 text-blue-600 rounded border-slate-300 focus:ring-blue-500"
                />
                <span className="text-xs md:text-sm font-semibold text-slate-700">
                  Tự động gom phần vật tư thiếu hụt vào Phiếu PR tổng hợp trong ngày
                </span>
              </label>
            </div>

            <div className="flex flex-wrap items-center gap-2.5 w-full md:w-auto justify-end">
              {/* Nút Tái phân bổ theo ETD */}
              <button
                type="button"
                onClick={handleReallocateByEtd}
                disabled={isReallocatingEtd || isAllocating}
                title="Phân bổ lại tồn kho khả dụng cho các đơn hàng theo thứ tự hạn giao hàng gần nhất"
                className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-bold rounded-lg transition disabled:opacity-50 cursor-pointer"
              >
                {isReallocatingEtd ? "Đang xử lý ETD..." : "Tái phân bổ theo ETD (FIFO)"}
              </button>

              {/* Nút Thực thi Khóa giữ chỗ (Primary xanh) HOẶC Hủy giữ chỗ (Danger đỏ) */}
              {spaceAllocationSummary.hasAnyAllocation ? (
                // Nút HỦY GIỮ CHỖ (Danger)
                <button
                  type="button"
                  onClick={handleReleaseAllocations}
                  disabled={
                    isReleasing ||
                    spaceAllocationSummary.isBlocked ||
                    isAllocating ||
                    isReallocatingEtd
                  }
                  title={
                    spaceAllocationSummary.isBlocked
                      ? spaceAllocationSummary.blockReason
                      : "Hủy toàn bộ vật tư đang giữ chỗ của đơn hàng và hoàn trả lại vào kho khả dụng"
                  }
                  className={`px-4 py-2 text-white text-xs font-bold rounded-lg shadow-sm transition flex items-center gap-2 ${
                    spaceAllocationSummary.isBlocked
                      ? "bg-rose-400 opacity-60 cursor-not-allowed"
                      : "bg-rose-600 hover:bg-rose-700 cursor-pointer"
                  }`}
                >
                  {isReleasing ? (
                    <span>Đang hủy giữ chỗ...</span>
                  ) : (
                    <>
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
                          d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                        />
                      </svg>
                      <span>
                        {spaceAllocationSummary.isBlocked
                          ? "Hủy giữ chỗ (Bị chặn)"
                          : "Hủy giữ chỗ vật tư"}
                      </span>
                    </>
                  )}
                </button>
              ) : (
                // Nút THỰC THI KHÓA GIỮ CHỖ (Primary xanh)
                <button
                  type="button"
                  onClick={handleExecuteAutoAllocation}
                  disabled={
                    isAllocating ||
                    isReallocatingEtd ||
                    !spaceInfo ||
                    (mrpData?.materialRequirements?.length || 0) === 0
                  }
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-lg shadow-sm transition disabled:opacity-50 flex items-center gap-2 cursor-pointer"
                >
                  {isAllocating ? (
                    <span>Đang khóa giữ chỗ...</span>
                  ) : (
                    <>
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
                          d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"
                        />
                      </svg>
                      <span>Thực thi Khóa giữ chỗ &amp; Gom PR</span>
                    </>
                  )}
                </button>
              )}
            </div>
          </div>
        )}

        {/* Bảng Chi tiết Đối chiếu Tồn kho Khả dụng (MRP Grid) */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          {/* Header Bảng & Bộ lọc */}
          <div className="px-6 py-4 border-b border-slate-200 flex flex-col md:flex-row md:items-center justify-between gap-3 bg-white">
            <div>
              <h2 className="text-base font-bold text-slate-900">
                Bảng phân tích nhu cầu &amp; Đối chiếu tồn kho 3 cấp cho {activeSpaceKey}
              </h2>
              <p className="text-xs text-slate-500 mt-0.5">
                Available = max(0, Physical - AllocatedOther). Net Shortage = Gross Demand - Reserved
                - Allocatable.
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2.5">
              <input
                type="text"
                value={searchKeyword}
                onChange={(e) => {
                  setSearchKeyword(e.target.value);
                  setPageNumber(1);
                }}
                placeholder="Tìm mã hoặc tên vật tư..."
                className="px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600 min-w-[200px]"
              />

              <select
                value={filterSupplyStatus}
                onChange={(e) => {
                  setFilterSupplyStatus(e.target.value);
                  setPageNumber(1);
                }}
                className="px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600 font-semibold"
              >
                <option value="ALL">Tất cả trạng thái</option>
                <option value="INSUFFICIENT">Chưa đủ nguyên liệu</option>
                <option value="SUFFICIENT">Đủ tồn kho</option>
                <option value="RESERVED">Đã giữ chỗ</option>
              </select>
            </div>
          </div>

          {/* Table Container */}
          <div className="overflow-x-auto bg-white">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-4 min-w-[190px]">Nguyên vật liệu</th>
                  <th className="py-3.5 px-3 text-right min-w-[105px]">Định mức tinh</th>
                  <th className="py-3.5 px-3 text-right min-w-[115px]">Nhu cầu có hao hụt</th>
                  <th className="py-3.5 px-3 text-right min-w-[100px]">Tồn thực tế</th>
                  <th className="py-3.5 px-3 text-right min-w-[105px]">Đã giữ đơn khác</th>
                  <th className="py-3.5 px-3 text-right min-w-[115px] bg-blue-50/40 text-blue-900">
                    Tồn khả dụng
                  </th>
                  <th className="py-3.5 px-3 text-right min-w-[105px]">Đã giữ cho đơn</th>
                  <th className="py-3.5 px-3 text-right min-w-[105px] text-emerald-800">
                    Khóa thêm ngay
                  </th>
                  <th className="py-3.5 px-3 text-right min-w-[110px] text-rose-700">
                    Thiếu hụt ròng
                  </th>
                  <th className="py-3.5 px-3 text-right min-w-[110px] text-slate-900">Đề xuất PR</th>
                  <th className="py-3.5 px-4 text-center min-w-[125px]">Trạng thái</th>
                  <th className="py-3.5 px-4 text-right min-w-[80px]">Chi tiết</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm bg-white">
                {isLoadingSpace || isLoadingMrp ? (
                  <tr>
                    <td colSpan={12} className="py-12 px-6 text-center text-slate-500">
                      Đang tính toán nhu cầu MRP và đối chiếu tồn kho khả dụng cho Không gian...
                    </td>
                  </tr>
                ) : spaceError || mrpError ? (
                  <tr>
                    <td colSpan={12} className="py-12 px-6 text-center text-rose-600 font-semibold">
                      {spaceError || mrpError}
                    </td>
                  </tr>
                ) : pagedMaterials.length === 0 ? (
                  <tr>
                    <td colSpan={12} className="py-12 px-6 text-center text-slate-500">
                      {spaceAllocations.length > 0 ? (
                        <div className="max-w-md mx-auto text-center py-2">
                          <div className="text-amber-700 font-bold mb-1">
                            Không gian này đang có {spaceAllocations.length} bản ghi giữ chỗ vật tư trực tiếp trong hệ thống ({fmtNum(spaceAllocationSummary.totalReserved)} đơn vị).
                          </div>
                          <div className="text-xs text-slate-500">
                            Các sản phẩm chưa được khai báo BOM để đối chiếu nhu cầu định mức. Bạn có thể bấm nút <strong className="text-rose-600">"Hủy giữ chỗ vật tư"</strong> ở trên để hoàn trả vật tư về kho khả dụng.
                          </div>
                        </div>
                      ) : (
                        "Không có dòng vật tư nào khớp với bộ lọc hiện tại."
                      )}
                    </td>
                  </tr>
                ) : (
                  pagedMaterials.map((m: MrpMaterialRequirementLineDto) => {
                    const isExpanded = expandedMaterialId === m.materialId;
                    const isShortage = m.netShortageQty > 0;

                    return (
                      <Fragment key={m.materialId}>
                        <tr
                          className={`hover:bg-slate-50/90 transition-colors ${
                            isShortage ? "bg-rose-50/20" : ""
                          }`}
                        >
                          {/* Tên & Mã vật tư */}
                          <td className="py-3.5 px-4">
                            <div className="font-bold text-slate-900 flex items-center gap-1.5">
                              <span>{m.materialName}</span>
                              {m.fscCertified && (
                                <span className="px-1.5 py-0.2 text-[10px] font-bold bg-emerald-100 text-emerald-800 rounded">
                                  FSC
                                </span>
                              )}
                            </div>
                            <div className="text-xs text-slate-500 font-mono mt-0.5">
                              {m.materialCode} • {m.materialGroupLabel || m.materialGroup}
                              {m.dimensionSpec ? ` • ${m.dimensionSpec}` : ""}
                            </div>
                          </td>

                          {/* Định mức tinh (Net BOM) */}
                          <td className="py-3.5 px-3 text-right font-mono text-slate-700">
                            {fmtNum(m.totalNetBomQty)} {m.unitOfMeasure}
                          </td>

                          {/* Tổng nhu cầu có hao hụt (Gross Demand) */}
                          <td className="py-3.5 px-3 text-right font-mono font-bold text-slate-900">
                            {fmtNum(m.totalGrossDemandQty)} {m.unitOfMeasure}
                          </td>

                          {/* Tồn thực tế (Physical Stock) */}
                          <td className="py-3.5 px-3 text-right font-mono text-slate-600">
                            {fmtNum(m.physicalStockQty)}
                          </td>

                          {/* Đã giữ cho đơn khác (Allocated to other orders) */}
                          <td className="py-3.5 px-3 text-right font-mono text-amber-700">
                            {fmtNum(m.allocatedToOtherOrdersQty)}
                          </td>

                          {/* Tồn khả dụng (Available Stock = Physical - AllocatedOther) */}
                          <td className="py-3.5 px-3 text-right font-mono font-bold text-blue-700 bg-blue-50/30">
                            {fmtNum(m.availableStockQty)}
                          </td>

                          {/* Đã giữ cho đơn này (Reserved for target) */}
                          <td className="py-3.5 px-3 text-right font-mono text-slate-700">
                            {fmtNum(m.alreadyReservedForTargetQty)}
                          </td>

                          {/* Khóa thêm ngay (Allocatable Now) */}
                          <td className="py-3.5 px-3 text-right font-mono font-semibold text-emerald-600">
                            {fmtNum(m.allocatableNowQty)}
                          </td>

                          {/* Thiếu hụt ròng (Net Shortage) */}
                          <td
                            className={`py-3.5 px-3 text-right font-mono font-bold ${
                              isShortage ? "text-rose-600 bg-rose-50/40" : "text-slate-400"
                            }`}
                          >
                            {fmtNum(m.netShortageQty)}
                          </td>

                          {/* Đề xuất đặt mua PR (Suggested PR) */}
                          <td className="py-3.5 px-3 text-right font-mono font-bold text-slate-900">
                            {fmtNum(m.suggestedPrOrderQty)} {m.unitOfMeasure}
                          </td>

                          {/* Trạng thái đáp ứng & Khóa giữ chỗ */}
                          <td className="py-3.5 px-4 text-center whitespace-nowrap">
                            <span
                              className={`inline-flex items-center px-2.5 py-0.5 text-xs font-bold rounded-full border ${
                                SUPPLY_BADGE_CLASS[m.supplyStatus] ??
                                "bg-slate-100 text-slate-700 border-slate-200"
                              }`}
                            >
                              {m.supplyStatusLabel ||
                                (m.supplyStatus === "RESERVED"
                                  ? "Đã giữ chỗ"
                                  : m.supplyStatus === "SUFFICIENT"
                                    ? "Đủ tồn kho"
                                    : "Chưa đủ nguyên liệu")}
                            </span>
                          </td>

                          {/* Nút xem chi tiết sản phẩm nào cần vật tư này */}
                          <td className="py-3.5 px-4 text-right whitespace-nowrap">
                            <button
                              type="button"
                              onClick={() =>
                                setExpandedMaterialId(isExpanded ? null : m.materialId)
                              }
                              className="text-xs font-semibold text-blue-600 hover:text-blue-800 cursor-pointer"
                            >
                              {m.itemBreakdowns?.length || 0} SP {isExpanded ? "▲" : "▼"}
                            </button>
                          </td>
                        </tr>

                        {/* Hàng mở rộng: Chi tiết từng dòng sản phẩm cần mã vật tư này */}
                        {isExpanded && (
                          <tr className="bg-slate-50/80">
                            <td colSpan={12} className="px-6 py-3 border-y border-slate-200">
                              <div className="text-xs font-bold text-slate-700 mb-2">
                                Phân bổ cho các dòng sản phẩm của vật tư: {m.materialName} (
                                {m.materialCode})
                              </div>
                              <table className="w-full text-xs text-left border-collapse bg-white rounded-lg border border-slate-200 overflow-hidden">
                                <thead>
                                  <tr className="bg-slate-100 text-slate-600 border-b border-slate-200">
                                    <th className="py-2 px-3">Mã đơn / Space</th>
                                    <th className="py-2 px-3">Sản phẩm</th>
                                    <th className="py-2 px-3 text-right">SL đặt</th>
                                    <th className="py-2 px-3 text-right">Hao hụt</th>
                                    <th className="py-2 px-3 text-right">Định mức/SP</th>
                                    <th className="py-2 px-3 text-right font-bold">Nhu cầu gộp</th>
                                    <th className="py-2 px-3 text-right text-emerald-700">
                                      Đã khóa (Reserved)
                                    </th>
                                    <th className="py-2 px-3 text-right text-rose-600 font-bold">
                                      Chưa đáp ứng
                                    </th>
                                    <th className="py-2 px-3">Hạn ETD</th>
                                    <th className="py-2 px-3">Trạng thái dòng</th>
                                  </tr>
                                </thead>
                                <tbody className="divide-y divide-slate-100">
                                  {m.itemBreakdowns?.map((ib) => (
                                    <tr key={ib.orderItemId} className="hover:bg-slate-50">
                                      <td className="py-1.5 px-3 font-mono">
                                        {ib.poNumber} {ib.spaceKey ? `(${ib.spaceKey})` : ""}
                                      </td>
                                      <td className="py-1.5 px-3 font-semibold text-slate-900">
                                        {ib.productSku} — {ib.productName}
                                      </td>
                                      <td className="py-1.5 px-3 text-right font-mono">
                                        {ib.orderedQty} cái
                                      </td>
                                      <td className="py-1.5 px-3 text-right font-mono text-slate-500">
                                        {ib.scrapRatePercent}%
                                      </td>
                                      <td className="py-1.5 px-3 text-right font-mono">
                                        {fmtNum(ib.netQtyPerUnit)} {m.unitOfMeasure}
                                      </td>
                                      <td className="py-1.5 px-3 text-right font-mono font-bold text-slate-900">
                                        {fmtNum(ib.grossDemandQty)} {m.unitOfMeasure}
                                      </td>
                                      <td className="py-1.5 px-3 text-right font-mono font-bold text-emerald-700">
                                        {fmtNum(ib.reservedQty)}
                                      </td>
                                      <td
                                        className={`py-1.5 px-3 text-right font-mono font-bold ${
                                          ib.unfulfilledDemandQty > 0
                                            ? "text-rose-600"
                                            : "text-slate-400"
                                        }`}
                                      >
                                        {fmtNum(ib.unfulfilledDemandQty)}
                                      </td>
                                      <td className="py-1.5 px-3 text-slate-600">
                                        {fmtDate(ib.etdDate)}
                                      </td>
                                      <td className="py-1.5 px-3">
                                        <span
                                          className={`inline-block px-1.5 py-0.2 rounded text-[11px] font-bold ${
                                            ib.orderItemStatus === "READY_PROD"
                                              ? "bg-emerald-100 text-emerald-800"
                                              : "bg-amber-100 text-amber-800"
                                          }`}
                                        >
                                          {ib.orderItemStatus}
                                        </span>
                                      </td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </td>
                          </tr>
                        )}
                      </Fragment>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* Thanh Phân trang Danh sách Vật tư (Pagination) */}
          {!isLoadingSpace && !isLoadingMrp && !spaceError && !mrpError && totalItems > 0 && (
            <div className="px-6 py-3.5 border-t border-slate-200 bg-[#F8FAFC] flex flex-col sm:flex-row items-center justify-between gap-3 text-sm text-slate-600">
              <span>
                Hiển thị <strong>{startItem}–{endItem}</strong> / <strong>{totalItems}</strong> mã vật tư
                {filterSupplyStatus !== "ALL" || searchKeyword.trim() ? " (đã lọc)" : ""}
              </span>
              <div className="flex items-center gap-2">
                <select
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setPageNumber(1);
                  }}
                  className="px-2 py-1.5 text-xs bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600 font-semibold"
                >
                  {[10, 20, 50].map((n) => (
                    <option key={n} value={n}>
                      {n} / trang
                    </option>
                  ))}
                </select>
                <button
                  type="button"
                  disabled={pageNumber <= 1}
                  onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                  className="px-3 py-1.5 bg-white border border-slate-300 rounded-lg text-xs font-semibold hover:bg-slate-50 transition disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
                >
                  Trước
                </button>
                <span className="text-xs font-semibold px-1">
                  {pageNumber} / {totalPages}
                </span>
                <button
                  type="button"
                  disabled={pageNumber >= totalPages}
                  onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
                  className="px-3 py-1.5 bg-white border border-slate-300 rounded-lg text-xs font-semibold hover:bg-slate-50 transition disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
                >
                  Sau
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}
