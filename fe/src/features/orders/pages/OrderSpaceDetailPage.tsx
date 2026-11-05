import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import WmmsAppShell from '../../../components/layout/WmmsAppShell';
import { useWmms } from '../../auth/hooks/WmmsContext';
import { bomStatusLabel, lifecycleStatusLabel } from '../../../lib/statusLabels';

/**
 * Màn hình III.4.3: Order Space Detail Dashboard (SCR-ORD-03)
 * Tuân thủ 100% thiết kế giao diện và bảng Field Description trong Report 3 (Section III.4.3):
 * - Header Top-Left (Field #1): `Active Order Space: SPACE-PO-880504 (Dining Sets - Ashley US)`
 * - Khối tiêu đề Space:
 *   + `SPACE-PO-880504: Ashley Furniture Homestore (USA)` + huy hiệu `IN_PRODUCTION` & `CHỨNG NHẬN FSC 100%`
 *   + Dòng thông tin: `Customer Ref: ASH-2026-OCT-880504 | Order Intake: Oct 12, 2026 | Target Delivery (ETD): Oct 28, 2026 (8 Days Remaining)`
 *   + Field #2: Nút màu cam `📐 Manage BOM & Routing →`
 *   + Field #3: Nút trắng `📋 View on Kanban`
 * - 4 Thẻ KPI:
 *   + `TỔNG SỐ LƯỢNG ĐẶT`: `500 Pcs` (`400 ghế ăn + 100 bàn`)
 *   + `TRẠNG THÁI BOM`: `Sẵn sàng 100%` (`Bóc tách đang hoạt động, tỷ lệ hao hụt 3,5%`)
 *   + `PHÂN BỔ VẬT TƯ`: `Đã khóa đủ` (`Tồn kho đã được Kho giữ chỗ`)
 *   + `TIẾN ĐỘ XƯỞNG`: `Hoàn thành 62%` (`Trạm 4: Đang lắp ráp kết cấu`)
 * - Bảng `Các dòng sản phẩm đặt hàng trong Space này` (Field #6):
 *   + Ghi chú góc phải: `Quy cách PO chỉ xem (được bảo vệ bởi RBAC)` (BR-ORD-04)
 *   + Các cột: `Mã SKU`, `Product Model & Specs`, `Quy cách hoàn thiện`, `Số lượng đặt`, `Bản vẽ CAD/PDF đính kèm` (Field #4), `Vòng đời BOM`, `Thao tác` (`Xem BOM →` - Field #5)
 * - Khối `Recent Space Activity & Milestones` (Field #7):
 *   + Dòng thời gian 3 mốc: `Đã xuất kho xuống xưởng`, `BOM & Routing Approved`, `Đã khởi tạo Order Space`.
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
            productName: 'Ghế ăn gỗ sồi phong cách Bắc Âu',
            specsSubtext: '450 x 520 x 850 mm | Gỗ sồi trắng Mỹ',
            finishSpec: 'Sơn phủ PU trong tự nhiên',
            orderQty: '400 cái',
            cadFile: 'CAD_Chair_880504_Rev2.pdf',
            bomStatus: 'READY_FOR_PRODUCTION'
          },
          {
            skuCode: 'TABLE-OAK-02',
            productName: 'Bàn ăn gỗ sồi nguyên khối 1,8 m',
            specsSubtext: '1800 x 900 x 750 mm | Gỗ sồi trắng Mỹ + Giá đỡ thép',
            finishSpec: 'Sơn phủ PU trong tự nhiên',
            orderQty: '100 cái',
            cadFile: 'CAD_Table_180_Rev1.pdf',
            bomStatus: 'READY_FOR_PRODUCTION'
          }
        ]
      : selectedSpace?.customRows?.length
      ? selectedSpace.customRows.map((r) => ({
          skuCode: r.productSku,
          productName: r.productDescription,
          specsSubtext: `${r.dimensionsMm} mm | Gỗ sồi trắng Mỹ`,
          finishSpec: r.finishColor,
          orderQty: `${r.orderQty} cái`,
          cadFile: r.technicalDrawingName || `CAD_${r.productSku}.pdf`,
          bomStatus: 'READY_FOR_PRODUCTION'
        }))
      : [
          {
            skuCode: 'CHAIR-OAK-01',
            productName: selectedSpace?.lineItemsVolumeSummary || 'Sản phẩm nội thất gỗ sồi nguyên khối',
            specsSubtext: '1200 x 600 x 450 mm | Gỗ sồi trắng Mỹ',
            finishSpec: 'Sơn phủ PU trong tự nhiên',
            orderQty: `${selectedSpace?.totalOrderedUnits || 250} cái`,
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
                {lifecycleStatusLabel(selectedSpace.lifecycleStatusBadge || 'IN_PRODUCTION')}
              </span>
              <span className="px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-emerald-100 text-emerald-700">
                CHỨNG NHẬN FSC 100%
              </span>
            </div>

            <div className="text-xs text-slate-500 mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1">
              <span>
                Mã tham chiếu khách:{' '}
                <strong className="text-slate-800">{selectedSpace.poNumber}</strong>
              </span>
              <span>|</span>
              <span>
                Ngày tiếp nhận: <strong className="text-slate-800">12/10/2026</strong>
              </span>
              <span>|</span>
              <span>
                Hạn giao (ETD):{' '}
                <strong className="text-rose-600">
                  28/10/2026 (còn {selectedSpace.daysRemaining} ngày)
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
              <span>Quản lý BOM &amp; quy trình →</span>
            </button>

            {/* Field #3: View on Kanban */}
            <button
              type="button"
              onClick={() => navigate('/orders')}
              className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition flex items-center gap-1.5"
            >
              <span>📋</span>
              <span>Xem trên Kanban</span>
            </button>
          </div>
        </div>

        {/* 4 Thẻ KPI (TOTAL ORDERED UNITS, BOM STATUS, MATERIAL ALLOCATION, FLOOR PROGRESS) */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              TỔNG SỐ LƯỢNG ĐẶT
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">
              {selectedSpace.totalOrderedUnits} cái
            </div>
            <div className="text-[11px] text-slate-400 mt-1">400 ghế ăn + 100 bàn</div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              TRẠNG THÁI BOM
            </div>
            <div className="text-2xl font-extrabold text-emerald-600 mt-1">Sẵn sàng 100%</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Bóc tách đang hoạt động, tỷ lệ hao hụt 3,5%
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              PHÂN BỔ VẬT TƯ
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">Đã khóa đủ</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Tồn kho đã được Kho giữ chỗ
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              TIẾN ĐỘ XƯỞNG
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">Hoàn thành 62%</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Trạm 4: Đang lắp ráp kết cấu
            </div>
          </div>
        </div>

        {/* Field #6: Bảng Ordered Product Line Items in this Space */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-5 py-3.5 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
            <h2 className="text-xs font-bold text-slate-800">
              Các dòng sản phẩm đặt hàng trong Space này
            </h2>
            <span className="text-[11px] text-slate-400">
              Quy cách PO chỉ xem (được bảo vệ bởi RBAC)
            </span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-5">Mã SKU</th>
                  <th className="py-3 px-4">Mẫu sản phẩm &amp; quy cách</th>
                  <th className="py-3 px-4">Quy cách hoàn thiện</th>
                  <th className="py-3 px-4">Số lượng đặt</th>
                  <th className="py-3 px-4">Bản vẽ CAD/PDF đính kèm</th>
                  <th className="py-3 px-4">Vòng đời BOM</th>
                  <th className="py-3 px-5 text-right">Thao tác</th>
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
                        {bomStatusLabel(item.bomStatus)}
                      </span>
                    </td>
                    <td className="py-3.5 px-5 text-right">
                      {/* Field #5: Inspect BOM → */}
                      <button
                        type="button"
                        onClick={() => navigate('/orders/staging')}
                        className="px-3 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50"
                      >
                        Xem BOM →
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
            Hoạt động gần đây &amp; Mốc của Space
          </h2>

          <div className="relative pl-5 border-l border-slate-200 space-y-5 text-xs">
            {/* Milestone 1 */}
            <div className="relative">
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 absolute -left-[25px] top-1 ring-4 ring-white" />
              <div className="text-slate-800">
                <strong className="font-bold">Đã xuất kho xuống xưởng</strong>{' '}
                <span className="text-slate-400">
                  — 16/10/2026 14:20 bởi Vu Duc Thang (Thủ kho)
                </span>
              </div>
              <p className="text-slate-500 text-[11px] mt-0.5">
                Đã bàn giao 100% gỗ và phụ kiện cho quản đốc sản xuất. Thẻ Kanban đã được mở khóa.
              </p>
            </div>

            {/* Milestone 2 */}
            <div className="relative">
              <span className="w-2.5 h-2.5 rounded-full bg-blue-600 absolute -left-[25px] top-1 ring-4 ring-white" />
              <div className="text-slate-800">
                <strong className="font-bold">BOM &amp; quy trình đã được duyệt</strong>{' '}
                <span className="text-slate-400">
                  — 13/10/2026 10:15 bởi Nguyen Van Khoa (Chủ xưởng)
                </span>
              </div>
              <p className="text-slate-500 text-[11px] mt-0.5">
                Đã cấu hình quy trình động 6 bước và tính tổng nhu cầu vật tư thô với
                hao hụt cho phép 3,5%.
              </p>
            </div>

            {/* Milestone 3 */}
            <div className="relative">
              <span className="w-2.5 h-2.5 rounded-full bg-slate-400 absolute -left-[25px] top-1 ring-4 ring-white" />
              <div className="text-slate-800">
                <strong className="font-bold">Đã khởi tạo Order Space</strong>{' '}
                <span className="text-slate-400">
                  — 12/10/2026 09:30 bởi Tran Thi Mai (Kinh doanh)
                </span>
              </div>
              <p className="text-slate-500 text-[11px] mt-0.5">
                PO đã được xác nhận qua khu Staging. Đã đính kèm bản vẽ CAD và quy cách đóng gói của người mua.
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
                Bản vẽ kỹ thuật CAD đính kèm
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
              Tệp: <strong className="font-mono">{previewAttachment}</strong>
            </p>
            <p className="text-[11px] text-slate-500">
              Đã xác minh URL kho lưu trữ riêng có chữ ký trước. Sẵn sàng xem trên máy tính bảng tại xưởng.
            </p>
            <div className="flex justify-end pt-2">
              <button
                type="button"
                onClick={() => setPreviewAttachment(null)}
                className="px-4 py-1.5 text-xs font-semibold text-white bg-blue-600 rounded-md"
              >
                Đóng xem trước
              </button>
            </div>
          </div>
        </div>
      )}
    </WmmsAppShell>
  );
}
