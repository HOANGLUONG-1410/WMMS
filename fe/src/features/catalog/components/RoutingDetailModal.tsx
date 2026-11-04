import { useDefaultRouting } from "../hooks/useCatalog";
import type { VisualCatalogProductDto } from "../types";

interface RoutingDetailModalProps {
  product: VisualCatalogProductDto | null;
  onClose: () => void;
}

export default function RoutingDetailModal({ product, onClose }: RoutingDetailModalProps) {
  const { data: stages, isLoading } = useDefaultRouting(product?.productId ?? null);

  if (!product) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
          <div>
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold px-2 py-0.5 rounded bg-blue-100 text-blue-700">
                {product.categoryName}
              </span>
              <span className="text-xs font-mono font-bold text-slate-500">
                {product.productSku}
              </span>
            </div>
            <h2 className="text-base font-bold text-slate-900 mt-1">
              Standard Production Routing — {product.productName}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 p-1.5 rounded-lg hover:bg-slate-200/60 transition text-lg leading-none"
          >
            ✕
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-4 max-h-[75vh] overflow-y-auto">
          {/* Thông tin tóm tắt sản phẩm */}
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 p-3 bg-slate-50 rounded-lg border border-slate-200 text-xs">
            <div>
              <span className="text-slate-400 block text-[11px]">Dimensions:</span>
              <span className="font-semibold text-slate-700">{product.dimensionsCm || "N/A"}</span>
            </div>
            <div>
              <span className="text-slate-400 block text-[11px]">Wood Species:</span>
              <span className="font-semibold text-slate-700">{product.woodSpecies || "N/A"}</span>
            </div>
            <div>
              <span className="text-slate-400 block text-[11px]">Finish / Color:</span>
              <span className="font-semibold text-slate-700">{product.finishColor || "N/A"}</span>
            </div>
          </div>

          <div>
            <h3 className="text-xs font-bold text-slate-700 uppercase tracking-wider mb-2">
              Sequential Production Stages &amp; Quality Gates:
            </h3>

            {isLoading && (
              <div className="py-8 text-center text-slate-400">
                <span className="inline-block w-5 h-5 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                <p className="text-xs">Loading standard routing sequence...</p>
              </div>
            )}

            {!isLoading && (!stages || stages.length === 0) && (
              <p className="py-6 text-center text-xs text-slate-400 italic">
                No default routing stages found for this product.
              </p>
            )}

            {!isLoading && stages && stages.length > 0 && (
              <div className="relative border-l-2 border-blue-200 ml-4 pl-4 space-y-3 py-1">
                {stages.map((stg) => (
                  <div key={stg.stageId} className="relative group">
                    {/* Bullet marker */}
                    <div
                      className={`absolute -left-[23px] top-1.5 w-3.5 h-3.5 rounded-full border-2 border-white shadow-xs ${
                        stg.isHoldPoint ? "bg-rose-500" : "bg-blue-600"
                      }`}
                    />

                    <div className="p-3 bg-white hover:bg-slate-50 rounded-lg border border-slate-200 transition flex items-center justify-between gap-3">
                      <div className="flex items-center gap-3">
                        <span className="w-6 h-6 rounded-full bg-slate-100 text-slate-700 font-bold text-xs flex items-center justify-center shrink-0">
                          {stg.stageSequence}
                        </span>
                        <div>
                          <p className="text-xs font-bold text-slate-800">{stg.stageName}</p>
                          <p className="text-[11px] text-slate-500">
                            100% Quality Inspection Gate before passing to next stage
                          </p>
                        </div>
                      </div>

                      {stg.isHoldPoint ? (
                        <span className="inline-flex items-center gap-1 px-2.5 py-1 text-[10px] font-extrabold uppercase rounded bg-rose-100 text-rose-700 border border-rose-200">
                          <span>🛑</span> Hold Point Stage
                        </span>
                      ) : (
                        <span className="text-[10px] font-semibold text-slate-400 uppercase">
                          Standard Stage
                        </span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="p-3 bg-blue-50/70 border border-blue-200 rounded-lg text-xs text-blue-900 leading-relaxed">
            <span className="font-bold text-blue-700">💡 Quy tắc chốt chặn kỹ thuật (Hold Point):</span>{" "}
            Tại các công đoạn được đánh dấu <span className="font-semibold text-rose-700">Hold Point</span>, nếu vật tư phụ kiện chưa được Thủ kho xuất cấp thực tế (`Goods Issue`), hệ thống sẽ tự động khóa cứng không cho phép phôi chạy tiếp.
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-3.5 border-t border-slate-200 bg-slate-50 flex justify-end">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-slate-800 hover:bg-slate-900 text-white text-xs font-semibold rounded-lg shadow-sm transition"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

