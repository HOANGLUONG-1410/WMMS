import { useState } from "react";
import { bomApi } from "../api/bomApi";
import type { BomDetailDto, RoutingStageDto } from "../types";

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
  routingStages?: RoutingStageDto[];
  bomDetails?: BomDetailDto[];
}

export const VISUAL_CATALOG_MODELS: VisualModelOption[] = [
  {
    id: 1,
    key: "solid-dining-chair",
    label: "Solid Dining Chair",
    sku: "CHAIR-OAK-01",
    productName: "Nordic Oak Dining Chair",
    dimensions: "450 x 520 x 850 mm",
    finishSpec: "Natural PU Clear",
    cadDrawing: "CAD_Chair_880504.pdf",
    orderQty: 400,
    icon: "🪑",
    categoryGroup: "SEATING",
    defaultScrapRate: 3.5,
  },
  {
    id: 2,
    key: "upholstered-chair",
    label: "Upholstered Chair",
    sku: "CHAIR-UPH-02",
    productName: "Fabric Upholstered Lounge Chair",
    dimensions: "540 x 580 x 820 mm",
    finishSpec: "Walnut Stain + Linen Fabric",
    cadDrawing: "CAD_UphChair_02.pdf",
    orderQty: 250,
    icon: "🛋️",
    categoryGroup: "SEATING",
    defaultScrapRate: 4.0,
  },
  {
    id: 3,
    key: "dining-bench",
    label: "Dining Bench",
    sku: "BENCH-OAK-03",
    productName: "Solid Oak Dining Bench 1.4m",
    dimensions: "1400 x 380 x 450 mm",
    finishSpec: "Natural PU Clear",
    cadDrawing: "CAD_Bench_140.pdf",
    orderQty: 150,
    icon: "🪵",
    categoryGroup: "SEATING",
    defaultScrapRate: 3.5,
  },
  {
    id: 4,
    key: "wooden-stool",
    label: "Wooden Stool",
    sku: "STOOL-OAK-04",
    productName: "Compact Kitchen Bar Stool",
    dimensions: "380 x 380 x 650 mm",
    finishSpec: "Natural Matte Finish",
    cadDrawing: "CAD_Stool_V1.pdf",
    orderQty: 300,
    icon: "🪑",
    categoryGroup: "SEATING",
    defaultScrapRate: 3.0,
  },
  {
    id: 5,
    key: "dining-table",
    label: "Dining Table",
    sku: "TABLE-OAK-02",
    productName: "Solid Oak Dining Table 1.8m",
    dimensions: "1800 x 900 x 750 mm",
    finishSpec: "Natural PU Clear Coat",
    cadDrawing: "CAD_Table_180_Rev1.pdf",
    orderQty: 100,
    icon: "🪵",
    categoryGroup: "TABLES_STORAGE",
    defaultScrapRate: 4.0,
  },
  {
    id: 6,
    key: "coffee-table",
    label: "Coffee Table",
    sku: "TABLE-COF-06",
    productName: "Nordic Oak Coffee Table",
    dimensions: "1200 x 600 x 420 mm",
    finishSpec: "Natural Oak Oil",
    cadDrawing: "CAD_CoffeeTable_06.pdf",
    orderQty: 250,
    icon: "☕",
    categoryGroup: "TABLES_STORAGE",
    defaultScrapRate: 3.5,
  },
  {
    id: 7,
    key: "wardrobe-2-door",
    label: "Wardrobe 2-Door",
    sku: "CABINET-WARD-07",
    productName: "Oak Veneer 2-Door Wardrobe",
    dimensions: "1100 x 600 x 2000 mm",
    finishSpec: "Warm White & Natural Oak",
    cadDrawing: "CAD_Wardrobe_2D.pdf",
    orderQty: 80,
    icon: "🚪",
    categoryGroup: "TABLES_STORAGE",
    defaultScrapRate: 4.5,
  },
  {
    id: 8,
    key: "queen-bed-frame",
    label: "Queen Bed Frame",
    sku: "BED-QUEEN-08",
    productName: "Queen Size Solid Wood Bed Frame",
    dimensions: "2100 x 1650 x 1050 mm",
    finishSpec: "European White Ash Clear",
    cadDrawing: "CAD_QueenBed_08.pdf",
    orderQty: 300,
    icon: "🛏️",
    categoryGroup: "TABLES_STORAGE",
    defaultScrapRate: 4.0,
  },
];

interface VisualProductCatalogModalProps {
  isOpen: boolean;
  currentModelKey: string;
  onClose: () => void;
  onSelectModel: (
    model: VisualModelOption,
    inheritedRouting?: RoutingStageDto[],
    inheritedDetails?: BomDetailDto[],
  ) => void;
}

export default function VisualProductCatalogModal({
  isOpen,
  currentModelKey,
  onClose,
  onSelectModel,
}: VisualProductCatalogModalProps) {
  const [selectedKey, setSelectedKey] = useState(currentModelKey || "solid-dining-chair");
  const [isApplying, setIsApplying] = useState(false);

  if (!isOpen) return null;

  const seatingModels = VISUAL_CATALOG_MODELS.filter((m) => m.categoryGroup === "SEATING");
  const tableStorageModels = VISUAL_CATALOG_MODELS.filter(
    (m) => m.categoryGroup === "TABLES_STORAGE",
  );

  const handleConfirmInherit = async () => {
    const chosen =
      VISUAL_CATALOG_MODELS.find((m) => m.key === selectedKey) || VISUAL_CATALOG_MODELS[0];
    setIsApplying(true);

    try {
      const res = await bomApi.checkProductBom(chosen.id);
      if (res?.data) {
        onSelectModel(
          chosen,
          res.data.routingStages?.length ? res.data.routingStages : undefined,
          res.data.details?.length ? res.data.details : undefined,
        );
        onClose();
        return;
      }
    } catch {
      // Fallback sang template chuẩn của model được chọn
    } finally {
      setIsApplying(false);
    }

    onSelectModel(chosen);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-xl overflow-hidden">
        {/* Modal Header */}
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
          <h2 className="text-sm font-extrabold text-slate-900">
            Visual Product Catalog Selection
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1.5 py-0.5 rounded"
          >
            ✕
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 space-y-5">
          <p className="text-xs text-slate-500">
            Select visual model hierarchy to auto-load benchmark BOM structures and routing
            templates:
          </p>

          {/* Category 1: Chairs & Seating */}
          <div>
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400 mb-2.5">
              CATEGORY: CHAIRS &amp; SEATING
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              {seatingModels.map((item) => {
                const isSelected = selectedKey === item.key;
                return (
                  <button
                    key={item.key}
                    type="button"
                    onClick={() => setSelectedKey(item.key)}
                    className={`p-3.5 rounded-lg border text-center transition flex flex-col items-center justify-center gap-2 ${
                      isSelected
                        ? "border-2 border-[#d97706] bg-amber-50/40 shadow-2xs"
                        : "border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50"
                    }`}
                  >
                    <span className="text-2xl">{item.icon}</span>
                    <span className="text-[11px] font-bold text-slate-800 leading-tight">
                      {item.label}
                    </span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Category 2: Tables & Storage */}
          <div>
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400 mb-2.5">
              CATEGORY: TABLES &amp; STORAGE
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              {tableStorageModels.map((item) => {
                const isSelected = selectedKey === item.key;
                return (
                  <button
                    key={item.key}
                    type="button"
                    onClick={() => setSelectedKey(item.key)}
                    className={`p-3.5 rounded-lg border text-center transition flex flex-col items-center justify-center gap-2 ${
                      isSelected
                        ? "border-2 border-[#d97706] bg-amber-50/40 shadow-2xs"
                        : "border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50"
                    }`}
                  >
                    <span className="text-2xl">{item.icon}</span>
                    <span className="text-[11px] font-bold text-slate-800 leading-tight">
                      {item.label}
                    </span>
                  </button>
                );
              })}
            </div>
          </div>
        </div>

        {/* Modal Footer */}
        <div className="px-6 py-3.5 border-t border-slate-200 bg-slate-50/50 flex items-center justify-end gap-2.5">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleConfirmInherit}
            disabled={isApplying}
            className="px-4 py-2 bg-[#d97706] hover:bg-amber-700 text-white text-xs font-semibold rounded-md shadow-sm transition disabled:opacity-60"
          >
            {isApplying ? "Loading Template..." : "Inherit Default Routing & BOM Template"}
          </button>
        </div>
      </div>
    </div>
  );
}

