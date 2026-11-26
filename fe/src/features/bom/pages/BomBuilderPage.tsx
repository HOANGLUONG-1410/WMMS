import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { ApiError } from "../../../lib/api/apiError";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useWmms } from "../../auth/hooks/WmmsContext";
import { orderIntakeApi } from "../../../services/wmmsApi";
import { bomApi } from "../api/bomApi";
import type { OrderSpaceLineItemForBom, RoutingStageDto } from "../types";
import DynamicRoutingModal from "../components/DynamicRoutingModal";
import ProductBomModal from "../components/ProductBomModal";

export default function BomBuilderPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { currentSpaceKey, setCurrentSpaceKey } = useWmms();

  const spaceParam = searchParams.get("spaceKey");
  const activeSpaceKey = spaceParam || currentSpaceKey || "";
  const orderItemIdParam = Number(searchParams.get("orderItemId")) || 0;

  const [spaceLineItems, setSpaceLineItems] = useState<OrderSpaceLineItemForBom[]>([]);
  const [spaceCustomerName, setSpaceCustomerName] = useState<string>("");
  const [spacePoNumber, setSpacePoNumber] = useState<string>("");
  const [isLoadingSpace, setIsLoadingSpace] = useState<boolean>(true);
  const [spaceError, setSpaceError] = useState<string | null>(null);

  // Modal tùy chỉnh quy trình sản xuất cho sản phẩm đang chọn
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
        "Không xác định được Không gian đơn hàng (Order Space). Vui lòng truy cập từ Danh sách đơn hàng.",
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
        }
      } else {
        setSpaceLineItems([]);
        setSpaceError(`Lỗi: Không tìm thấy thông tin Không gian đơn hàng "${spaceKey}".`);
      }
    } catch (err: any) {
      setSpaceLineItems([]);
      setSpaceError(
        err?.response?.data?.message ||
          err?.message ||
          `Không thể tải thông tin sản phẩm của Không gian đơn hàng "${spaceKey}" từ máy chủ.`,
      );
    } finally {
      setIsLoadingSpace(false);
    }
  };

  useEffect(() => {
    loadOrderSpaceLineItems(activeSpaceKey);
  }, [activeSpaceKey]);

  const selectedItem =
    orderItemIdParam > 0
      ? spaceLineItems.find((x) => x.orderItemId === orderItemIdParam) || null
      : null;

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
    } catch (err: any) {
      setToastBanner({
        type: "error",
        text:
          err?.response?.data?.message ||
          err?.message ||
          `Không thể tải quy trình sản xuất của sản phẩm ${item.productSku} từ máy chủ.`,
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
      if (err instanceof ApiError) {
        msg = err.message || msg;
      } else if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setToastBanner({
        type: "error",
        text: msg,
      });
    }
  };

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
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M10 19l-7-7m0 0l7-7m-7 7h18"
              />
            </svg>
            <span>{activeSpaceKey ? "Danh sách sản phẩm" : "Danh sách đơn hàng"}</span>
          </button>

        </div>
      </HeaderTopLeft>
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
        <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-xs flex flex-col lg:flex-row lg:items-center justify-between gap-4">
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
              {selectedItem
                ? `Lập BOM & Quy trình SX: ${selectedItem.productSku}`
                : "Lập BOM & Quy trình sản xuất"}
            </h1>
          </div>

          <div className="flex flex-wrap items-center gap-2.5 shrink-0">
            {activeSpaceKey && (
              <button
                type="button"
                onClick={() => navigate(`/orders/spaces/${encodeURIComponent(activeSpaceKey)}`)}
                className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg shadow-2xs transition cursor-pointer"
              >
                ← Danh sách sản phẩm
              </button>
            )}

            {activeSpaceKey && (
              <button
                type="button"
                onClick={() =>
                  navigate(`/inventory/reconciliation?spaceKey=${encodeURIComponent(activeSpaceKey)}`)
                }
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
              >
                <span>Đối chiếu tồn kho &amp; Khóa vật tư →</span>
              </button>
            )}
          </div>
        </div>

        {/* Nội dung trang Lập BOM riêng cho sản phẩm đang chọn */}
        {isLoadingSpace ? (
          <div className="bg-white rounded-xl border border-slate-200 p-12 text-center text-sm text-slate-500 shadow-xs">
            Đang tải dữ liệu sản phẩm và định mức BOM...
          </div>
        ) : spaceError || !selectedItem ? (
          <div className="px-5 py-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-sm font-semibold flex flex-wrap items-center justify-between gap-4 shadow-xs">
            <span>
              {spaceError ||
                (orderItemIdParam > 0
                  ? `Không tìm thấy sản phẩm #${orderItemIdParam} trong Không gian đơn hàng "${activeSpaceKey}".`
                  : "Chưa chọn sản phẩm cụ thể. Vui lòng chọn sản phẩm từ Danh sách sản phẩm trong Không gian đơn hàng.")}
            </span>
            <button
              type="button"
              onClick={() =>
                navigate(
                  activeSpaceKey
                    ? `/orders/spaces/${encodeURIComponent(activeSpaceKey)}`
                    : "/orders",
                )
              }
              className="px-3.5 py-1.5 bg-white hover:bg-rose-100 text-rose-700 border border-rose-300 text-xs font-bold rounded-lg shrink-0 cursor-pointer"
            >
              {activeSpaceKey ? "Về Danh sách sản phẩm" : "Về Danh sách đơn hàng"}
            </button>
          </div>
        ) : (
          <ProductBomModal
            variant="page"
            isOpen={true}
            lineItem={selectedItem}
            spaceKey={activeSpaceKey}
            onClose={() =>
              navigate(
                activeSpaceKey
                  ? `/orders/spaces/${encodeURIComponent(activeSpaceKey)}`
                  : "/orders",
              )
            }
            onSaved={() => {
              if (activeSpaceKey) {
                loadOrderSpaceLineItems(activeSpaceKey);
              }
            }}
            onOpenRouting={() => handleOpenRoutingModal(selectedItem)}
            isLoadingRouting={isLoadingRouting}
          />
        )}
      </div>

      {/* Modal: Tùy chỉnh quy trình sản xuất */}
      <DynamicRoutingModal
        isOpen={Boolean(routingModalItem)}
        stages={routingStages}
        onClose={() => setRoutingModalItem(null)}
        onSave={handleSaveProductRouting}
      />
    </>
  );
}
