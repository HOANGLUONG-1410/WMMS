import { useEffect, useMemo, useState } from "react";
import { bomApi } from "../api/bomApi";
import { useVisualCatalogForBom } from "../hooks/useBom";
import type { BomDetailDto, RoutingStageDto, VisualCatalogProductDto } from "../types";

export interface VisualModelOption {
  id: number;
  key: string;
  label: string;
  sku: string;
  productName: string;
  dimensions: string;
  finishSpec: string;
  cadDrawing: string;
  orderQty: number;
  icon: string;
  categoryGroup: "SEATING" | "TABLES_STORAGE";
  defaultScrapRate: number;
  hasActiveBom?: boolean;
  routingStages?: RoutingStageDto[];
  bomDetails?: BomDetailDto[];
}

export const VISUAL_CATALOG_MODELS: VisualModelOption[] = [
  {
    id: 1,
    key: "chair-dining-oak-01",
    label: "Ghế ăn gỗ nguyên khối",
    sku: "CHAIR-DINING-OAK-01",
    productName: "Ghế ăn tựa nan gỗ sồi Bắc Mỹ",
    dimensions: "45 x 52 x 88 cm",
    finishSpec: "Óc chó tự nhiên",
    cadDrawing: "CAD_Chair_880504.pdf",
    orderQty: 400,
    icon: "🪑",
    categoryGroup: "SEATING",
    defaultScrapRate: 3.5,
  },
  {
    id: 2,
    key: "table-dining-ext-02",
    label: "Bàn ăn",
    sku: "TABLE-DINING-EXT-02",
    productName: "Bàn ăn thông minh kéo dài gỗ sồi",
    dimensions: "160/210 x 90 x 75 cm",
    finishSpec: "Sồi xông khói",
    cadDrawing: "CAD_Table_180_Rev1.pdf",
    orderQty: 150,
    icon: "🪵",
    categoryGroup: "TABLES_STORAGE",
    defaultScrapRate: 4.0,
  },
  {
    id: 3,
    key: "cabinet-sideboard-03",
    label: "Tủ buffet",
    sku: "CABINET-SIDEBOARD-03",
    productName: "Tủ sideboard 3 cánh 2 hộc kéo",
    dimensions: "150 x 42 x 82 cm",
    finishSpec: "Hạt dẻ ấm",
    cadDrawing: "CAD_Sideboard_03.pdf",
    orderQty: 100,
    icon: "🚪",
    categoryGroup: "TABLES_STORAGE",
    defaultScrapRate: 4.0,
  },
];

function mapBackendProductToVisualModel(p: VisualCatalogProductDto): VisualModelOption {
  const catUpper = (p.categoryName || "").toUpperCase();
  const isSeating =
    catUpper.includes("CHAIR") ||
    catUpper.includes("SEAT") ||
    catUpper.includes("BENCH") ||
    catUpper.includes("STOOL");
  const icon = catUpper.includes("CHAIR")
    ? "🪑"
    : catUpper.includes("TABLE")
      ? "🪵"
      : catUpper.includes("CABINET") || catUpper.includes("WARDROBE")
        ? "🚪"
        : "🛏️";

  return {
    id: p.productId,
    key: p.productSku.toLowerCase(),
    label: p.productName,
    sku: p.productSku,
    productName: p.productName,
    dimensions: p.dimensionsCm || "Quy cách chuẩn",
    finishSpec: p.finishColor || p.woodSpecies || "Hoàn thiện xuất khẩu chuẩn",
    cadDrawing: p.cadDrawingUrl || `CAD_${p.productSku}.pdf`,
    orderQty: 200,
    icon,
    categoryGroup: isSeating ? "SEATING" : "TABLES_STORAGE",
    defaultScrapRate: 3.5,
    hasActiveBom: p.hasActiveBom,
    routingStages: p.defaultRoutingStages?.length ? p.defaultRoutingStages : undefined,
  };
}

interface VisualProductCatalogModalProps {
  isOpen: boolean;
  currentModelKey: string;
  onClose: () => void;
  onSelectModel: (
    model: VisualModelOption,
    inheritedRouting?: RoutingStageDto[],
    inheritedDetails?: BomDetailDto[],
    inheritedScrapRate?: number,
  ) => void;
}

export default function VisualProductCatalogModal({
  isOpen,
  currentModelKey,
  onClose,
  onSelectModel,
}: VisualProductCatalogModalProps) {
  const { data: backendCatalog, isLoading } = useVisualCatalogForBom();

  const catalogModels = useMemo<VisualModelOption[]>(() => {
    if (backendCatalog && backendCatalog.length > 0) {
      return backendCatalog.map(mapBackendProductToVisualModel);
    }
    return VISUAL_CATALOG_MODELS;
  }, [backendCatalog]);

  const [selectedKey, setSelectedKey] = useState(
    currentModelKey || catalogModels[0]?.key || "chair-dining-oak-01",
  );
  const [isApplying, setIsApplying] = useState(false);

  useEffect(() => {
    if (isOpen && catalogModels.length > 0) {
      const exists = catalogModels.some((m) => m.key === currentModelKey);
      setSelectedKey(exists ? currentModelKey : catalogModels[0].key);
    }
  }, [isOpen, currentModelKey, catalogModels]);

  if (!isOpen) return null;

  const seatingModels = catalogModels.filter((m) => m.categoryGroup === "SEATING");
  const tableStorageModels = catalogModels.filter((m) => m.categoryGroup === "TABLES_STORAGE");

  const handleConfirmInherit = async () => {
    const chosen = catalogModels.find((m) => m.key === selectedKey) || catalogModels[0];
    setIsApplying(true);

    try {
      const res = await bomApi.checkProductBom(chosen.id);
      let resolvedRouting = res?.data?.routingStages?.length
        ? res.data.routingStages
        : chosen.routingStages;

      if (!resolvedRouting || resolvedRouting.length === 0) {
        try {
          const routingRes = await bomApi.getDefaultRouting(chosen.id);
          if (routingRes?.data?.length) {
            resolvedRouting = routingRes.data;
          }
        } catch {
          // Sử dụng routing sẵn có trên model
        }
      }

      onSelectModel(
        chosen,
        resolvedRouting,
        res?.data?.details?.length ? res.data.details : undefined,
        res?.data?.scrapRatePercent || chosen.defaultScrapRate,
      );
      onClose();
      return;
    } catch {
      // Fallback sang thông tin của model được chọn nếu mất kết nối
    } finally {
      setIsApplying(false);
    }

    onSelectModel(chosen, chosen.routingStages, chosen.bomDetails, chosen.defaultScrapRate);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-2xl overflow-hidden">
        {/* Modal Header */}
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
          <div>
            <h2 className="text-sm font-extrabold text-slate-900">
              Chọn từ danh mục sản phẩm trực quan (CSDL máy chủ)
            </h2>          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1.5 py-0.5 rounded"
          >
            ✕
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 space-y-5 max-h-[70vh] overflow-y-auto">
          {isLoading ? (
            <div className="py-8 text-center text-xs text-slate-500">
              Đang tải danh mục sản phẩm trực quan từ máy chủ...
            </div>
          ) : (
            <>
              <p className="text-xs text-slate-500">
                Chọn một mẫu sản phẩm từ danh mục của xưởng để kế thừa BOM hiện có
                và chuỗi công đoạn mặc định tại các trạm:
              </p>

              {/* Category 1: Chairs & Seating */}
              {seatingModels.length > 0 && (
                <div>
                  <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400 mb-2.5">
                    NHÓM: GHẾ &amp; CHỖ NGỒI
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                    {seatingModels.map((item) => {
                      const isSelected = selectedKey === item.key;
                      return (
                        <button
                          key={item.key}
                          type="button"
                          onClick={() => setSelectedKey(item.key)}
                          className={`p-3.5 rounded-lg border text-left transition flex flex-col justify-between gap-2 ${
                            isSelected
                              ? "border-2 border-[#d97706] bg-amber-50/40 shadow-2xs"
                              : "border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50"
                          }`}
                        >
                          <div className="flex items-center justify-between w-full">
                            <span className="text-2xl">{item.icon}</span>
                            {item.hasActiveBom && (
                              <span className="px-1.5 py-0.5 text-[9px] font-extrabold uppercase rounded bg-emerald-100 text-emerald-700">
                                BOM đang dùng
                              </span>
                            )}
                          </div>
                          <div>
                            <div className="font-mono text-[10px] font-bold text-amber-700">
                              {item.sku}
                            </div>
                            <div className="text-xs font-bold text-slate-900 leading-tight mt-0.5">
                              {item.productName}
                            </div>
                            <div className="text-[10px] text-slate-500 mt-1">
                              {item.dimensions} • {item.finishSpec}
                            </div>
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Category 2: Tables & Storage */}
              {tableStorageModels.length > 0 && (
                <div>
                  <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400 mb-2.5">
                    NHÓM: BÀN &amp; TỦ LƯU TRỮ
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                    {tableStorageModels.map((item) => {
                      const isSelected = selectedKey === item.key;
                      return (
                        <button
                          key={item.key}
                          type="button"
                          onClick={() => setSelectedKey(item.key)}
                          className={`p-3.5 rounded-lg border text-left transition flex flex-col justify-between gap-2 ${
                            isSelected
                              ? "border-2 border-[#d97706] bg-amber-50/40 shadow-2xs"
                              : "border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50"
                          }`}
                        >
                          <div className="flex items-center justify-between w-full">
                            <span className="text-2xl">{item.icon}</span>
                            {item.hasActiveBom && (
                              <span className="px-1.5 py-0.5 text-[9px] font-extrabold uppercase rounded bg-emerald-100 text-emerald-700">
                                BOM đang dùng
                              </span>
                            )}
                          </div>
                          <div>
                            <div className="font-mono text-[10px] font-bold text-amber-700">
                              {item.sku}
                            </div>
                            <div className="text-xs font-bold text-slate-900 leading-tight mt-0.5">
                              {item.productName}
                            </div>
                            <div className="text-[10px] text-slate-500 mt-1">
                              {item.dimensions} • {item.finishSpec}
                            </div>
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </div>
              )}
            </>
          )}
        </div>

        {/* Modal Footer */}
        <div className="px-6 py-3.5 border-t border-slate-200 bg-slate-50/50 flex items-center justify-end gap-2.5">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition"
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={handleConfirmInherit}
            disabled={isApplying}
            className="px-4 py-2 bg-[#d97706] hover:bg-amber-700 text-white text-xs font-semibold rounded-md shadow-sm transition disabled:opacity-60"
          >
            {isApplying ? "Đang truy vấn BOM từ máy chủ..." : "Kế thừa quy trình mặc định & mẫu BOM"}
          </button>
        </div>
      </div>
    </div>
  );
}
