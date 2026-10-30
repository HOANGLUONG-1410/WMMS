import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import WmmsAppShell from '../../../components/layout/WmmsAppShell';
import { useWmms } from '../../auth/hooks/WmmsContext';

/**
 * Màn hình III.4.3: Order Space Detail Dashboard (SCR-ORD-03)
 * Tuân thủ 100% thiết kế giao diện và bảng Field Description trong Report 3 (Section III.4.3):
 * - Header Top-Left (Field #1): `Active Order Space: SPACE-PO-880504 (Dining Sets - Ashley US)`
 * - Khối tiêu đề Space:
 *   + `SPACE-PO-880504: Ashley Furniture Homestore (USA)` + huy hiệu `IN_PRODUCTION` & `FSC 100% CERTIFIED`
 *   + Dòng thông tin: `Customer Ref: ASH-2026-OCT-880504 | Order Intake: Oct 12, 2026 | Target Delivery (ETD): Oct 28, 2026 (8 Days Remaining)`
 *   + Field #2: Nút màu cam `📐 Manage BOM & Routing →`
 *   + Field #3: Nút trắng `📋 View on Kanban`
 * - 4 Thẻ KPI:
 *   + `TOTAL ORDERED UNITS`: `500 Pcs` (`400 Dining Chairs + 100 Tables`)
 *   + `BOM STATUS`: `100% Ready` (`Active breakdown with 3.5% scrap rate`)
 *   + `MATERIAL ALLOCATION`: `Fully Locked` (`Allocated Stock secured by Warehouse`)
 *   + `FLOOR PROGRESS`: `62% Done` (`Station 4: Structure Assembly in progress`)
 * - Bảng `Ordered Product Line Items in this Space` (Field #6):
 *   + Ghi chú góc phải: `View-only PO specifications (Protected by RBAC)` (BR-ORD-04)
 *   + Các cột: `SKU Code`, `Product Model & Specs`, `Finish Spec`, `Order Quantity`, `Attached Engineering CAD/PDF` (Field #4), `BOM Lifecycle`, `Action` (`Inspect BOM →` - Field #5)
 * - Khối `Recent Space Activity & Milestones` (Field #7):
 *   + Dòng thời gian 3 mốc: `Goods Issue Released to Floor`, `BOM & Routing Approved`, `Order Space Initialized`.
 */
export default function OrderSpaceDetailPage() {
  const { spaceKey } = useParams();
  const navigate = useNavigate();
  const { orderSpaces, currentSpaceKey, setCurrentSpaceKey } = useWmms();
  const [previewAttachment, setPreviewAttachment] = useState(null);

  const activeKey = spaceKey || currentSpaceKey || 'SPACE-PO-880504';

  useEffect(() => {
    if (spaceKey && spaceKey !== currentSpaceKey) {
      setCurrentSpaceKey(spaceKey);
    }
  }, [spaceKey, currentSpaceKey, setCurrentSpaceKey]);

  const selectedSpace =
    orderSpaces.find((s) => s.spaceKey === activeKey) || orderSpaces[0];

  // Dữ liệu chi tiết sản phẩm trong Order Space (khớp 100% với SCR-ORD-03 cho SPACE-PO-880504 hoặc linh hoạt cho Space khác)
  const lineItems =
    selectedSpace?.spaceKey === 'SPACE-PO-880504'
      ? [
          {
            skuCode: 'CHAIR-OAK-01',
            productName: 'Nordic Oak Dining Chair',
            specsSubtext: '450 x 520 x 850 mm | American White Oak',
            finishSpec: 'Natural PU Clear Coat',
            orderQty: '400 Pcs',
            cadFile: 'CAD_Chair_880504_Rev2.pdf',
            bomStatus: 'READY_FOR_PRODUCTION'
          },
          {
            skuCode: 'TABLE-OAK-02',
            productName: 'Solid Oak Dining Table 1.8m',
            specsSubtext: '1800 x 900 x 750 mm | American White Oak + Steel Bracket',
            finishSpec: 'Natural PU Clear Coat',
            orderQty: '100 Pcs',
            cadFile: 'CAD_Table_180_Rev1.pdf',
            bomStatus: 'READY_FOR_PRODUCTION'
          }
        ]
      : selectedSpace?.customRows?.length
      ? selectedSpace.customRows.map((r) => ({
          skuCode: r.productSku,
          productName: r.productDescription,
          specsSubtext: `${r.dimensionsMm} mm | American White Oak`,
          finishSpec: r.finishColor,
          orderQty: `${r.orderQty} Pcs`,
          cadFile: r.technicalDrawingName || `CAD_${r.productSku}.pdf`,
          bomStatus: 'READY_FOR_PRODUCTION'
        }))
      : [
          {
            skuCode: 'CHAIR-OAK-01',
            productName: selectedSpace?.lineItemsVolumeSummary || 'Solid Oak Furniture Item',
            specsSubtext: '1200 x 600 x 450 mm | American White Oak',
            finishSpec: 'Natural PU Clear Coat',
            orderQty: `${selectedSpace?.totalOrderedUnits || 250} Pcs`,
            cadFile: `CAD_${selectedSpace?.spaceKey || '880504'}_Rev1.pdf`,
            bomStatus: 'READY_FOR_PRODUCTION'
          }
        ];

  return (
    <WmmsAppShell mode="owner">
      <div className="max-w-[1400px] mx-auto space-y-5">
        {/* Khung Header của Order Space + Field #2 & Field #3 */}
        <div className="bg-white rounded-lg border border-slate-200 p-5 shadow-sm flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="text-base font-extrabold text-slate-900 tracking-tight">
                {selectedSpace.spaceKey}: {selectedSpace.customerName}
              </h1>
              <span className="px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-blue-100 text-blue-700">
                {selectedSpace.lifecycleStatusBadge || 'IN_PRODUCTION'}
              </span>
              <span className="px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-emerald-100 text-emerald-700">
                FSC 100% CERTIFIED
              </span>
            </div>

            <div className="text-xs text-slate-500 mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1">
              <span>
                Customer Ref:{' '}
                <strong className="text-slate-800">{selectedSpace.poNumber}</strong>
              </span>
              <span>|</span>
              <span>
                Order Intake: <strong className="text-slate-800">Oct 12, 2026</strong>
              </span>
              <span>|</span>
              <span>
                Target Delivery (ETD):{' '}
                <strong className="text-rose-600">
                  Oct 28, 2026 ({selectedSpace.daysRemaining} Days Remaining)
                </strong>
              </span>
            </div>
          </div>

          <div className="flex items-center gap-2.5 shrink-0">
            {/* Field #2: Manage BOM & Routing → */}
            <button
              type="button"
              onClick={() => navigate('/orders/staging')}
              className="px-3.5 py-2 bg-[#d97706] hover:bg-amber-700 text-white text-xs font-semibold rounded-md shadow-sm transition flex items-center gap-1.5"
            >
              <span>📐</span>
              <span>Manage BOM &amp; Routing →</span>
            </button>

            {/* Field #3: View on Kanban */}
            <button
              type="button"
              onClick={() => navigate('/orders')}
              className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition flex items-center gap-1.5"
            >
              <span>📋</span>
              <span>View on Kanban</span>
            </button>
          </div>
        </div>

        {/* 4 Thẻ KPI (TOTAL ORDERED UNITS, BOM STATUS, MATERIAL ALLOCATION, FLOOR PROGRESS) */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              TOTAL ORDERED UNITS
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">
              {selectedSpace.totalOrderedUnits} Pcs
            </div>
            <div className="text-[11px] text-slate-400 mt-1">400 Dining Chairs + 100 Tables</div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              BOM STATUS
            </div>
            <div className="text-2xl font-extrabold text-emerald-600 mt-1">100% Ready</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Active breakdown with 3.5% scrap rate
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              MATERIAL ALLOCATION
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">Fully Locked</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Allocated Stock secured by Warehouse
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              FLOOR PROGRESS
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">62% Done</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Station 4: Structure Assembly in progress
            </div>
          </div>
        </div>

        {/* Field #6: Bảng Ordered Product Line Items in this Space */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-5 py-3.5 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
            <h2 className="text-xs font-bold text-slate-800">
              Ordered Product Line Items in this Space
            </h2>
            <span className="text-[11px] text-slate-400">
              View-only PO specifications (Protected by RBAC)
            </span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-5">SKU Code</th>
                  <th className="py-3 px-4">Product Model &amp; Specs</th>
                  <th className="py-3 px-4">Finish Spec</th>
                  <th className="py-3 px-4">Order Quantity</th>
                  <th className="py-3 px-4">Attached Engineering CAD/PDF</th>
                  <th className="py-3 px-4">BOM Lifecycle</th>
                  <th className="py-3 px-5 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-xs">
                {lineItems.map((item, idx) => (
                  <tr key={idx} className="hover:bg-slate-50/80 transition-colors">
                    <td className="py-3.5 px-5 font-mono font-bold text-slate-800">
                      {item.skuCode}
                    </td>
                    <td className="py-3.5 px-4">
                      <div className="font-bold text-slate-900">{item.productName}</div>
                      <div className="text-[11px] text-slate-400 mt-0.5">{item.specsSubtext}</div>
                    </td>
                    <td className="py-3.5 px-4 text-slate-600">{item.finishSpec}</td>
                    <td className="py-3.5 px-4 font-bold text-slate-900">{item.orderQty}</td>
                    <td className="py-3.5 px-4">
                      {/* Field #4: Attached Engineering CAD/PDF Link */}
                      <button
                        type="button"
                        onClick={() => setPreviewAttachment(item.cadFile)}
                        className="text-blue-600 hover:text-blue-700 hover:underline font-medium flex items-center gap-1"
                      >
                        <span>📐</span>
                        <span>{item.cadFile}</span>
                      </button>
                    </td>
                    <td className="py-3.5 px-4">
                      <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-emerald-100 text-emerald-700">
                        {item.bomStatus}
                      </span>
                    </td>
                    <td className="py-3.5 px-5 text-right">
                      {/* Field #5: Inspect BOM → */}
                      <button
                        type="button"
                        onClick={() => navigate('/orders/staging')}
                        className="px-3 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50"
                      >
                        Inspect BOM →
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Field #7: Recent Space Activity & Milestones */}
        <div className="bg-white rounded-lg border border-slate-200 p-5 shadow-sm">
          <h2 className="text-xs font-bold text-slate-800 mb-4">
            Recent Space Activity &amp; Milestones
          </h2>

          <div className="relative pl-5 border-l border-slate-200 space-y-5 text-xs">
            {/* Milestone 1 */}
            <div className="relative">
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 absolute -left-[25px] top-1 ring-4 ring-white" />
              <div className="text-slate-800">
                <strong className="font-bold">Goods Issue Released to Floor</strong>{' '}
                <span className="text-slate-400">
                  — Oct 16, 2026 14:20 by Vu Duc Thang (Warehouse)
                </span>
              </div>
              <p className="text-slate-500 text-[11px] mt-0.5">
                Handed over 100% lumber and hardware to Production Leader. Kanban card unlocked.
              </p>
            </div>

            {/* Milestone 2 */}
            <div className="relative">
              <span className="w-2.5 h-2.5 rounded-full bg-blue-600 absolute -left-[25px] top-1 ring-4 ring-white" />
              <div className="text-slate-800">
                <strong className="font-bold">BOM &amp; Routing Approved</strong>{' '}
                <span className="text-slate-400">
                  — Oct 13, 2026 10:15 by Nguyen Van Khoa (Owner)
                </span>
              </div>
              <p className="text-slate-500 text-[11px] mt-0.5">
                Configured 6-step dynamic routing and calculated gross raw material demand with
                3.5% loss allowance.
              </p>
            </div>

            {/* Milestone 3 */}
            <div className="relative">
              <span className="w-2.5 h-2.5 rounded-full bg-slate-400 absolute -left-[25px] top-1 ring-4 ring-white" />
              <div className="text-slate-800">
                <strong className="font-bold">Order Space Initialized</strong>{' '}
                <span className="text-slate-400">
                  — Oct 12, 2026 09:30 by Tran Thi Mai (Sales)
                </span>
              </div>
              <p className="text-slate-500 text-[11px] mt-0.5">
                PO confirmed via Staging Area. Attached CAD drawings and buyer packaging specs.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Modal xem trước bản vẽ kỹ thuật CAD/PDF khi bấm vào link Field #4 */}
      {previewAttachment && (
        <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-white rounded-xl shadow-2xl border border-slate-200 p-5 space-y-3">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-slate-900">
                Engineering CAD Drawing Attachment
              </h3>
              <button
                type="button"
                onClick={() => setPreviewAttachment(null)}
                className="text-slate-400 hover:text-slate-600 font-bold"
              >
                ×
              </button>
            </div>
            <p className="text-xs text-slate-600">
              File: <strong className="font-mono">{previewAttachment}</strong>
            </p>
            <p className="text-[11px] text-slate-500">
              Pre-signed private storage URL verified (SI-03). Ready for shop-floor tablet viewing.
            </p>
            <div className="flex justify-end pt-2">
              <button
                type="button"
                onClick={() => setPreviewAttachment(null)}
                className="px-4 py-1.5 text-xs font-semibold text-white bg-blue-600 rounded-md"
              >
                Close Preview
              </button>
            </div>
          </div>
        </div>
      )}
    </WmmsAppShell>
  );
}
