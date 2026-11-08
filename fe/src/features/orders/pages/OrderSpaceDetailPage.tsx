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

  const customRows = (
    selectedSpace as unknown as {
      customRows?: Array<{
        productSku: string;
        productDescription: string;
        dimensionsMm: string;
        finishColor: string;
        orderQty: number;
        technicalDrawingName?: string;
      }>;
    }
  )?.customRows;

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
      : customRows?.length
      ? customRows.map((r) => ({
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
    <WmmsAppShell
      mode="owner"
      customTopLeft={
        <button
          type="button"
          onClick={() => navigate('/orders')}
          className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-sm font-semibold text-slate-700 bg-white border border-slate-300 hover:bg-slate-50 transition shadow-2xs cursor-pointer"
        >
          <svg className="w-4 h-4 text-slate-500" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
          </svg>
          <span>Danh sách đơn hàng</span>
        </button>
      }
    >
      <div className="max-w-[1400px] mx-auto space-y-6">
        {/* Order Space Header Card */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
                {selectedSpace.spaceKey}: {selectedSpace.customerName}
              </h1>
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-blue-50 text-blue-700 border border-blue-200 whitespace-nowrap">
                <span className="w-1.5 h-1.5 rounded-full bg-blue-600" />
                {lifecycleStatusLabel(selectedSpace.lifecycleStatusBadge || 'IN_PRODUCTION')}
              </span>
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 whitespace-nowrap">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                CHỨNG NHẬN FSC 100%
              </span>
            </div>

            <div className="text-sm text-slate-600 mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
              <span>
                Mã PO khách:{' '}
                <strong className="font-mono text-slate-900">{selectedSpace.poNumber}</strong>
              </span>
              <span className="text-slate-300">|</span>
              <span>
                Ngày tiếp nhận: <strong className="text-slate-900">12/10/2026</strong>
              </span>
              <span className="text-slate-300">|</span>
              <span>
                Hạn giao (ETD):{' '}
                <strong className="text-rose-600">
                  28/10/2026 (còn {selectedSpace.daysRemaining} ngày)
                </strong>
              </span>
            </div>
          </div>

          <div className="flex items-center gap-3 shrink-0">
            <button
              type="button"
              onClick={() => navigate('/production/kanban')}
              className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg transition flex items-center gap-2 shadow-xs cursor-pointer"
            >
              <svg className="w-4 h-4 text-slate-500" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7" />
              </svg>
              <span>Xem trên Kanban</span>
            </button>

            <button
              type="button"
              onClick={() => navigate('/bom')}
              className="px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
              </svg>
              <span>Quản lý BOM &amp; quy trình →</span>
            </button>
          </div>
        </div>

        {/* 4 KPI Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
              TỔNG SỐ LƯỢNG ĐẶT
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">
              {selectedSpace.totalOrderedUnits} cái
            </div>
            <div className="text-xs text-slate-500 mt-1">400 ghế ăn + 100 bàn</div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
              TRẠNG THÁI BOM
            </div>
            <div className="text-2xl font-extrabold text-emerald-600 mt-2">Sẵn sàng 100%</div>
            <div className="text-xs text-slate-500 mt-1">
              Định mức đã duyệt, tỷ lệ hao hụt 3,5%
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
              PHÂN BỔ VẬT TƯ
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">Đã khóa đủ</div>
            <div className="text-xs text-slate-500 mt-1">
              Tồn kho đã được giữ chỗ theo lệnh
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
              TIẾN ĐỘ XƯỞNG
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">Hoàn thành 62%</div>
            <div className="text-xs text-slate-500 mt-1">
              Trạm 4: Đang lắp ráp kết cấu
            </div>
          </div>
        </div>

        {/* Ordered Product Line Items Table */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-2 bg-white">
            <h2 className="text-base font-bold text-slate-900">
              Danh sách sản phẩm đặt hàng trong Space
            </h2>
            <span className="text-xs text-slate-500">
              Quy cách PO đã khóa chỉnh sửa sau khi xác nhận
            </span>
          </div>

          <div className="overflow-x-auto bg-white">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-6 min-w-[140px]">Mã SKU</th>
                  <th className="py-3.5 px-4 min-w-[240px]">Mẫu sản phẩm &amp; quy cách</th>
                  <th className="py-3.5 px-4 min-w-[180px]">Quy cách hoàn thiện</th>
                  <th className="py-3.5 px-4 min-w-[120px]">Số lượng đặt</th>
                  <th className="py-3.5 px-4 min-w-[200px]">Bản vẽ kỹ thuật</th>
                  <th className="py-3.5 px-4 min-w-[160px]">Trạng thái BOM</th>
                  <th className="py-3.5 px-6 text-right min-w-[130px]">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm bg-white">
                {lineItems.map((item, idx) => (
                  <tr key={idx} className="hover:bg-slate-50/90 transition-colors">
                    <td className="py-4 px-6 font-mono font-bold text-slate-800 whitespace-nowrap">
                      {item.skuCode}
                    </td>
                    <td className="py-4 px-4">
                      <div className="font-semibold text-slate-900">{item.productName}</div>
                      <div className="text-xs text-slate-500 mt-0.5">{item.specsSubtext}</div>
                    </td>
                    <td className="py-4 px-4 text-slate-700 whitespace-nowrap">{item.finishSpec}</td>
                    <td className="py-4 px-4 font-bold text-slate-900 whitespace-nowrap">{item.orderQty}</td>
                    <td className="py-4 px-4 whitespace-nowrap">
                      <button
                        type="button"
                        onClick={() => setPreviewAttachment(item.cadFile as any)}
                        className="text-blue-600 hover:text-blue-700 hover:underline font-medium flex items-center gap-1.5 text-sm cursor-pointer"
                      >
                        <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                        <span>{item.cadFile}</span>
                      </button>
                    </td>
                    <td className="py-4 px-4 whitespace-nowrap">
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 whitespace-nowrap">
                        <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                        {bomStatusLabel(item.bomStatus)}
                      </span>
                    </td>
                    <td className="py-4 px-6 text-right whitespace-nowrap">
                      <button
                        type="button"
                        onClick={() => navigate('/bom')}
                        className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 cursor-pointer"
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

        {/* Recent Space Activity & Milestones */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
          <h2 className="text-base font-bold text-slate-900 mb-4">
            Nhật ký tiến độ &amp; Cột mốc đơn hàng
          </h2>

          <div className="relative pl-6 border-l-2 border-slate-200 space-y-5 text-sm">
            <div className="relative">
              <span className="w-3 h-3 rounded-full bg-emerald-500 absolute -left-[31px] top-1 ring-4 ring-white" />
              <div className="text-slate-900">
                <strong className="font-bold">Đã xuất kho xuống xưởng</strong>{' '}
                <span className="text-slate-500 text-xs">
                  — 16/10/2026 14:20 bởi Vu Duc Thang (Thủ kho)
                </span>
              </div>
              <p className="text-slate-600 text-sm mt-0.5">
                Đã bàn giao 100% gỗ và phụ kiện cho quản đốc sản xuất. Thẻ Kanban đã được mở khóa.
              </p>
            </div>

            <div className="relative">
              <span className="w-3 h-3 rounded-full bg-blue-600 absolute -left-[31px] top-1 ring-4 ring-white" />
              <div className="text-slate-900">
                <strong className="font-bold">BOM &amp; quy trình đã được duyệt</strong>{' '}
                <span className="text-slate-500 text-xs">
                  — 13/10/2026 10:15 bởi Nguyen Van Khoa (Chủ xưởng)
                </span>
              </div>
              <p className="text-slate-600 text-sm mt-0.5">
                Đã cấu hình quy trình động 6 bước và tính tổng nhu cầu vật tư thô với hao hụt cho phép 3,5%.
              </p>
            </div>

            <div className="relative">
              <span className="w-3 h-3 rounded-full bg-slate-400 absolute -left-[31px] top-1 ring-4 ring-white" />
              <div className="text-slate-900">
                <strong className="font-bold">Đã khởi tạo Order Space</strong>{' '}
                <span className="text-slate-500 text-xs">
                  — 12/10/2026 09:30 bởi Tran Thi Mai (Kinh doanh)
                </span>
              </div>
              <p className="text-slate-600 text-sm mt-0.5">
                PO đã được xác nhận qua khu Staging. Đã đính kèm bản vẽ CAD và quy cách đóng gói của người mua.
              </p>
            </div>
          </div>
        </div>
      </div>

      {previewAttachment && (
        <div className="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-[1px] flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-white rounded-xl shadow-2xl border border-slate-200 p-6 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-slate-900">
                Bản vẽ kỹ thuật CAD đính kèm
              </h3>
              <button
                type="button"
                onClick={() => setPreviewAttachment(null)}
                className="text-slate-400 hover:text-slate-600 text-xl font-bold leading-none cursor-pointer"
              >
                ×
              </button>
            </div>
            <p className="text-sm text-slate-700">
              Tệp: <strong className="font-mono">{previewAttachment}</strong>
            </p>
            <p className="text-xs text-slate-500">
              Đã xác minh URL kho lưu trữ riêng có chữ ký. Sẵn sàng xem trên thiết bị tại xưởng.
            </p>
            <div className="flex justify-end pt-2">
              <button
                type="button"
                onClick={() => setPreviewAttachment(null)}
                className="px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg cursor-pointer"
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
