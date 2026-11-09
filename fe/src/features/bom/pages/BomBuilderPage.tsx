import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useWmms } from "../../auth/hooks/WmmsContext";
import { orderIntakeApi } from "../../../services/wmmsApi";
import { bomApi } from "../api/bomApi";
import type { OrderSpaceLineItemForBom, RoutingStageDto } from "../types";
import DynamicRoutingModal from "../components/DynamicRoutingModal";
import ProductBomModal from "../components/ProductBomModal";

export default function BomBuilderPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { currentSpaceKey, setCurrentSpaceKey } = useWmms();

  const spaceParam = searchParams.get("spaceKey");
  const activeSpaceKey = spaceParam || currentSpaceKey || "";

  const [spaceLineItems, setSpaceLineItems] = useState<OrderSpaceLineItemForBom[]>([]);
  const [spaceCustomerName, setSpaceCustomerName] = useState<string>("");
  const [spacePoNumber, setSpacePoNumber] = useState<string>("");
  const [isLoadingSpace, setIsLoadingSpace] = useState<boolean>(true);
  const [spaceError, setSpaceError] = useState<string | null>(null);

  // Modal 1: Tùy chỉnh BOM cho sản phẩm đang chọn
  const [bomModalItem, setBomModalItem] = useState<OrderSpaceLineItemForBom | null>(null);

  // Modal 2: Tùy chỉnh quy trình sản xuất cho sản phẩm đang chọn
  const [routingModalItem, setRoutingModalItem] = useState<OrderSpaceLineItemForBom | null>(null);
  const [routingStages, setRoutingStages] = useState<RoutingStageDto[]>([]);
  const [isLoadingRouting, setIsLoadingRouting] = useState<boolean>(false);

  const [toastBanner, setToastBanner] = useState<{
    type: "success" | "error" | "info";
    text: string;
  } | null>(null);

  useEffect(() => {
    if (spaceParam && spaceParam !== currentSpaceKey) {
      setCurrentSpaceKey(spaceParam);
    }
  }, [spaceParam, currentSpaceKey, setCurrentSpaceKey]);

  const loadOrderSpaceLineItems = async (spaceKey: string) => {
    if (!spaceKey) {
      setIsLoadingSpace(false);
      setSpaceLineItems([]);
      setSpaceError(
        "Không xác định được Không gian đơn hàng (Order Space). Vui lòng truy cập từ Danh sách đơn hàng bán.",
      );
      return;
    }

    setIsLoadingSpace(true);
    setSpaceError(null);
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

        if (items.length === 0) {
          setSpaceError(
            `Lỗi dữ liệu: Không tìm thấy bất kỳ sản phẩm nào trong Không gian đơn hàng "${spaceKey}".`,
          );
        } else {
          // Nếu có ?orderItemId= trên URL khi chuyển từ trang chi tiết Space sang thì mở luôn modal BOM của sản phẩm đó
          const urlItemId = Number(searchParams.get("orderItemId")) || 0;
          if (urlItemId > 0) {
            const matched = items.find((x) => x.orderItemId === urlItemId);
            if (matched) {
              setBomModalItem(matched);
            }
          }
        }
      } else {
        setSpaceLineItems([]);
        setSpaceError(`Lỗi: Không tìm thấy thông tin Không gian đơn hàng "${spaceKey}".`);
      }
    } catch {
      setSpaceLineItems([]);
      setSpaceError(
        `Không thể tải danh sách sản phẩm của Không gian đơn hàng "${spaceKey}" từ máy chủ.`,
      );
    } finally {
      setIsLoadingSpace(false);
    }
  };

  useEffect(() => {
    loadOrderSpaceLineItems(activeSpaceKey);
  }, [activeSpaceKey]);

  const handleOpenBomModal = (item: OrderSpaceLineItemForBom) => {
    setBomModalItem(item);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("orderItemId", String(item.orderItemId));
      if (activeSpaceKey) next.set("spaceKey", activeSpaceKey);
      return next;
    });
  };

  const handleCloseBomModal = () => {
    setBomModalItem(null);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete("orderItemId");
      return next;
    });
  };

  const handleOpenRoutingModal = async (item: OrderSpaceLineItemForBom) => {
    setIsLoadingRouting(true);
    setToastBanner(null);
    try {
      let stages: RoutingStageDto[] = [];
      try {
        const res = await bomApi.getProductRouting(item.productId);
        if (res?.data?.length) {
          stages = res.data;
        }
      } catch {
        // Nếu chưa có routing tùy chỉnh riêng, lấy default routing
      }

      if (stages.length === 0) {
        const defRes = await bomApi.getDefaultRouting(item.productId);
        stages = defRes?.data || [];
      }

      setRoutingStages(stages);
      setRoutingModalItem(item);
    } catch {
      setToastBanner({
        type: "error",
        text: `Không thể tải quy trình sản xuất của sản phẩm ${item.productSku} từ máy chủ.`,
      });
    } finally {
      setIsLoadingRouting(false);
    }
  };

  const handleSaveProductRouting = async (updatedStages: RoutingStageDto[]) => {
    if (!routingModalItem) return;
    try {
      const res = await bomApi.saveProductRouting(routingModalItem.productId, updatedStages);
      setRoutingStages(res?.data || updatedStages);
      setToastBanner({
        type: "success",
        text:
          res?.message ||
          `Đã lưu cấu hình quy trình sản xuất cho sản phẩm ${routingModalItem.productSku} — ${routingModalItem.productName}.`,
      });
    } catch (err) {
      let msg = `Không thể lưu quy trình sản xuất cho sản phẩm ${routingModalItem.productSku}.`;
      if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({
        type: "error",
        text: msg,
      });
    }
  };

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
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M10 19l-7-7m0 0l7-7m-7 7h18"
              />
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
        </div>
      }
    >
      <div className="max-w-[1400px] mx-auto space-y-6">
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

        {/* Header + Active Order Space Info */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2.5">
              <span className="px-2.5 py-1 text-xs font-bold uppercase rounded-md bg-blue-50 text-blue-700 border border-blue-200 font-mono">
                {activeSpaceKey || "CHƯA CHỌN SPACE"}
              </span>
              {spacePoNumber && (
                <span className="text-sm text-slate-600">
                  Mã PO: <strong className="font-mono text-slate-900">{spacePoNumber}</strong>
                </span>
              )}
              {spaceCustomerName && (
                <span className="text-sm text-slate-600">
                  • Khách hàng: <strong className="text-slate-900">{spaceCustomerName}</strong>
                </span>
              )}
            </div>
            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight mt-2">
              Danh sách sản phẩm lập BOM &amp; Quy trình sản xuất
            </h1>
            <p className="text-sm text-slate-600 mt-1">
              Chọn từng sản phẩm trong Không gian đơn hàng bên dưới để mở cửa sổ{" "}
              <strong>Tùy chỉnh BOM</strong> hoặc <strong>Tùy chỉnh quy trình</strong>.
            </p>
          </div>

          <div className="flex items-center gap-3 shrink-0">
            <div className="px-4 py-2.5 rounded-lg bg-slate-50 border border-slate-200 text-sm text-slate-700">
              Tổng số sản phẩm:{" "}
              <strong className="font-extrabold text-slate-900">{spaceLineItems.length}</strong>
            </div>
          </div>
        </div>

        {/* Error Notification if No Product Data */}
        {!isLoadingSpace && (spaceError || spaceLineItems.length === 0) && (
          <div className="px-5 py-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm font-semibold flex items-center justify-between gap-4 shadow-xs">
            <span>
              {spaceError ||
                `Lỗi dữ liệu: Không tìm thấy sản phẩm nào trong Không gian đơn hàng "${activeSpaceKey}".`}
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

        {/* Product List Table in Active Order Space */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-wrap items-center justify-between gap-3 bg-white">
            <h2 className="text-base font-bold text-slate-900">
              Sản phẩm thuộc Không gian đơn hàng {activeSpaceKey}
            </h2>
            {isLoadingRouting && (
              <span className="text-xs font-semibold text-blue-600">
                Đang tải cấu hình quy trình sản xuất...
              </span>
            )}
          </div>

          <div className="overflow-x-auto bg-white">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-6 min-w-[100px]">Dòng hàng</th>
                  <th className="py-3.5 px-4 min-w-[140px]">Mã SKU</th>
                  <th className="py-3.5 px-4 min-w-[260px]">Tên sản phẩm &amp; Quy cách</th>
                  <th className="py-3.5 px-4 min-w-[120px]">Số lượng đặt</th>
                  <th className="py-3.5 px-4 min-w-[170px]">Bản vẽ kỹ thuật</th>
                  <th className="py-3.5 px-4 min-w-[160px]">Trạng thái BOM</th>
                  <th className="py-3.5 px-6 text-right min-w-[290px]">Thao tác tùy chỉnh</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm bg-white">
                {isLoadingSpace ? (
                  <tr>
                    <td colSpan={7} className="py-12 px-6 text-center text-slate-500">
                      Đang tải danh sách sản phẩm của Không gian đơn hàng...
                    </td>
                  </tr>
                ) : spaceLineItems.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-12 px-6 text-center text-rose-600 font-semibold">
                      {spaceError ||
                        "Không có dữ liệu sản phẩm nào trong Không gian đơn hàng này."}
                    </td>
                  </tr>
                ) : (
                  spaceLineItems.map((li) => {
                    const isReady =
                      li.itemStatus === "READY_PROD" ||
                      (li.bomLifecycleBadge || "").includes("LOCKED") ||
                      (li.bomLifecycleBadge || "").includes("READY");
                    const hasDraft = Boolean(li.bomId) && !isReady;

                    return (
                      <tr
                        key={li.orderItemId}
                        className="hover:bg-slate-50/90 transition-colors"
                      >
                        <td className="py-4 px-6 font-mono font-bold text-slate-600 whitespace-nowrap">
                          #{li.orderItemId}
                        </td>
                        <td className="py-4 px-4 font-mono font-bold text-slate-900 whitespace-nowrap">
                          {li.productSku}
                        </td>
                        <td className="py-4 px-4">
                          <div className="font-semibold text-slate-900">{li.productName}</div>
                          <div className="text-xs text-slate-500 mt-0.5">
                            {li.dimensionsSpec} | {li.woodSpecies} | {li.finishSpec}
                          </div>
                        </td>
                        <td className="py-4 px-4 font-bold text-slate-900 whitespace-nowrap">
                          {li.orderedQty} cái
                        </td>
                        <td className="py-4 px-4 font-mono text-xs text-slate-700 whitespace-nowrap">
                          {li.attachedCadFileName || "Chưa đính kèm"}
                        </td>
                        <td className="py-4 px-4 whitespace-nowrap">
                          <span
                            className={`inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full border whitespace-nowrap ${
                              isReady
                                ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                                : hasDraft
                                  ? "bg-amber-50 text-amber-800 border-amber-200"
                                  : "bg-slate-100 text-slate-600 border-slate-200"
                            }`}
                          >
                            <span
                              className={`w-1.5 h-1.5 rounded-full ${
                                isReady
                                  ? "bg-emerald-500"
                                  : hasDraft
                                    ? "bg-amber-500"
                                    : "bg-slate-400"
                              }`}
                            />
                            {isReady
                              ? "SẴN SÀNG SX"
                              : hasDraft
                                ? `BOM #${li.bomId} NHÁP`
                                : "CHƯA CÓ BOM"}
                          </span>
                        </td>
                        <td className="py-4 px-6 text-right whitespace-nowrap">
                          <div className="inline-flex items-center justify-end gap-2">
                            <button
                              type="button"
                              onClick={() => handleOpenBomModal(li)}
                              className="px-3.5 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold rounded-lg shadow-2xs transition cursor-pointer"
                            >
                              Tùy chỉnh BOM
                            </button>
                            <button
                              type="button"
                              onClick={() => handleOpenRoutingModal(li)}
                              disabled={isLoadingRouting}
                              className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-lg transition disabled:opacity-50 cursor-pointer"
                            >
                              Tùy chỉnh quy trình
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

      {/* Modal 1: Tùy chỉnh BOM */}
      <ProductBomModal
        isOpen={Boolean(bomModalItem)}
        lineItem={bomModalItem}
        spaceKey={activeSpaceKey}
        onClose={handleCloseBomModal}
        onSaved={() => {
          if (activeSpaceKey) {
            loadOrderSpaceLineItems(activeSpaceKey);
          }
        }}
      />

      {/* Modal 2: Tùy chỉnh quy trình sản xuất */}
      <DynamicRoutingModal
        isOpen={Boolean(routingModalItem)}
        stages={routingStages}
        onClose={() => setRoutingModalItem(null)}
        onSave={handleSaveProductRouting}
      />
    </WmmsAppShell>
  );
}
