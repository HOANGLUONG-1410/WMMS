import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import WmmsAppShell from '../../../components/layout/WmmsAppShell';
import { useWmms } from '../../auth/hooks/WmmsContext';
import { bomStatusLabel, lifecycleStatusLabel } from '../../../lib/statusLabels';
import { orderIntakeApi } from '../../../services/wmmsApi';
import type { OrderSpaceLineItemForBom } from '../../bom/types';

interface OrderSpaceMilestoneDto {
  title: string;
  timestamp: string;
  actorName: string;
  actorRole: string;
  description: string;
}

interface OrderSpaceDetailData {
  spaceId?: number;
  spaceKey: string;
  spaceName?: string;
  orderId?: number;
  poNumber: string;
  customerName: string;
  orderDate?: string;
  etdDate?: string;
  daysRemaining?: number;
  isUrgentEtd?: boolean;
  orderStatus?: string;
  lifecycleStatusBadge?: string;
  fscCertified100?: boolean;
  totalOrderedUnits?: number;
  totalOrderedSubtitle?: string;
  bomStatusTitle?: string;
  bomStatusSubtitle?: string;
  materialAllocationTitle?: string;
  materialAllocationSubtitle?: string;
  floorProgressPct?: number;
  floorProgressSubtitle?: string;
  lineItems?: OrderSpaceLineItemForBom[];
  milestones?: OrderSpaceMilestoneDto[];
}

/**
 * Màn hình III.4.3: Order Space Detail Dashboard (SCR-ORD-03)
 */
export default function OrderSpaceDetailPage() {
  const { spaceKey } = useParams();
  const navigate = useNavigate();
  const { orderSpaces, currentSpaceKey, setCurrentSpaceKey } = useWmms();
  const [previewAttachment, setPreviewAttachment] = useState<string | null>(null);

  const activeKey = spaceKey || currentSpaceKey || orderSpaces[0]?.spaceKey || '';

  const [spaceDetail, setSpaceDetail] = useState<OrderSpaceDetailData | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (spaceKey && spaceKey !== currentSpaceKey) {
      setCurrentSpaceKey(spaceKey);
    }
  }, [spaceKey, currentSpaceKey, setCurrentSpaceKey]);

  useEffect(() => {
    if (!activeKey) {
      setIsLoading(false);
      setLoadError('Không xác định được mã Không gian đơn hàng (Order Space). Vui lòng chọn từ Danh sách đơn hàng bán.');
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setLoadError(null);

    orderIntakeApi
      .getOrderSpaceDetail(activeKey)
      .then((res: { data?: OrderSpaceDetailData }) => {
        if (cancelled) return;
        const data = res?.data;
        if (data) {
          setSpaceDetail(data);
        } else {
          setSpaceDetail(null);
          setLoadError(`Không tìm thấy dữ liệu Không gian đơn hàng "${activeKey}".`);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSpaceDetail(null);
          setLoadError(`Không thể tải dữ liệu Không gian đơn hàng "${activeKey}" từ máy chủ.`);
        }
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [activeKey]);

  const fallbackSpace = orderSpaces.find((s) => s.spaceKey === activeKey);
  const lineItems = spaceDetail?.lineItems || [];
  const milestones = spaceDetail?.milestones || [];

  const getBomBadgeStyle = (status: string) => {
    const s = (status || '').toUpperCase();
    if (s.includes('READY') || s.includes('APPROVED') || s.includes('LOCKED')) {
      return {
        badge: 'bg-emerald-50 text-emerald-700 border-emerald-200',
        dot: 'bg-emerald-500',
      };
    }
    if (s.includes('DRAFT') || s.includes('PENDING')) {
      return {
        badge: 'bg-amber-50 text-amber-700 border-amber-200',
        dot: 'bg-amber-500',
      };
    }
    return {
      badge: 'bg-slate-100 text-slate-700 border-slate-300',
      dot: 'bg-slate-400',
    };
  };

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
        {loadError && (
          <div className="px-4 py-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-800 text-sm font-medium">
            {loadError}
          </div>
        )}

        {/* Order Space Header Card */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
                {spaceDetail?.spaceKey || fallbackSpace?.spaceKey || activeKey}:{' '}
                {spaceDetail?.customerName || fallbackSpace?.customerName || 'Đang tải...'}
              </h1>
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-blue-50 text-blue-700 border border-blue-200 whitespace-nowrap">
                <span className="w-1.5 h-1.5 rounded-full bg-blue-600" />
                {lifecycleStatusLabel(
                  spaceDetail?.lifecycleStatusBadge ||
                    fallbackSpace?.lifecycleStatusBadge ||
                    'BOM_SETTING'
                )}
              </span>
              {(spaceDetail?.fscCertified100 ?? true) && (
                <span className="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 whitespace-nowrap">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                  CHỨNG NHẬN FSC 100%
                </span>
              )}
            </div>

            <div className="text-sm text-slate-600 mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
              <span>
                Mã PO khách:{' '}
                <strong className="font-mono text-slate-900">
                  {spaceDetail?.poNumber || fallbackSpace?.poNumber || '—'}
                </strong>
              </span>
              <span className="text-slate-300">|</span>
              <span>
                Ngày tiếp nhận:{' '}
                <strong className="text-slate-900">
                  {spaceDetail?.orderDate
                    ? new Date(spaceDetail.orderDate).toLocaleDateString('vi-VN')
                    : '—'}
                </strong>
              </span>
              <span className="text-slate-300">|</span>
              <span>
                Hạn giao (ETD):{' '}
                <strong className="text-rose-600">
                  {spaceDetail?.etdDate
                    ? new Date(spaceDetail.etdDate).toLocaleDateString('vi-VN')
                    : fallbackSpace?.etdDate || '—'}{' '}
                  ({spaceDetail?.daysRemaining ?? fallbackSpace?.daysRemaining ?? 0} ngày)
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
              onClick={() => navigate(`/bom?spaceKey=${encodeURIComponent(activeKey)}`)}
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
              {spaceDetail?.totalOrderedUnits ?? fallbackSpace?.totalOrderedUnits ?? 0} cái
            </div>
            <div className="text-xs text-slate-500 mt-1">
              {spaceDetail?.totalOrderedSubtitle || `${lineItems.length} dòng sản phẩm`}
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
              TRẠNG THÁI BOM
            </div>
            <div className="text-2xl font-extrabold text-emerald-600 mt-2">
              {spaceDetail?.bomStatusTitle || 'Đang kiểm tra'}
            </div>
            <div className="text-xs text-slate-500 mt-1">
              {spaceDetail?.bomStatusSubtitle || 'Cập nhật từ định mức sản phẩm'}
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
              PHÂN BỔ VẬT TƯ
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">
              {spaceDetail?.materialAllocationTitle || 'Chưa phân bổ'}
            </div>
            <div className="text-xs text-slate-500 mt-1">
              {spaceDetail?.materialAllocationSubtitle || 'Theo dõi giữ chỗ tồn kho'}
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
              TIẾN ĐỘ XƯỞNG
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">
              Hoàn thành {spaceDetail?.floorProgressPct ?? 0}%
            </div>
            <div className="text-xs text-slate-500 mt-1">
              {spaceDetail?.floorProgressSubtitle || 'Tiến độ sản xuất tại xưởng'}
            </div>
          </div>
        </div>

        {/* Ordered Product Line Items Table */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-2 bg-white">
            <h2 className="text-base font-bold text-slate-900">
              Danh sách sản phẩm đặt hàng trong Space ({lineItems.length} sản phẩm)
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
                {isLoading ? (
                  <tr>
                    <td colSpan={7} className="py-10 px-6 text-center text-slate-500">
                      Đang tải danh sách sản phẩm trong Không gian đơn hàng...
                    </td>
                  </tr>
                ) : lineItems.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-10 px-6 text-center text-rose-600 font-semibold">
                      Lỗi: Không tìm thấy dữ liệu sản phẩm nào trong Không gian đơn hàng này.
                    </td>
                  </tr>
                ) : (
                  lineItems.map((item) => {
                    const statusText = item.bomLifecycleBadge || item.itemStatus || 'PENDING';
                    const badgeStyle = getBomBadgeStyle(statusText);
                    return (
                      <tr key={item.orderItemId} className="hover:bg-slate-50/90 transition-colors">
                        <td className="py-4 px-6 font-mono font-bold text-slate-800 whitespace-nowrap">
                          {item.productSku}
                        </td>
                        <td className="py-4 px-4">
                          <div className="font-semibold text-slate-900">{item.productName}</div>
                          <div className="text-xs text-slate-500 mt-0.5">
                            {item.dimensionsSpec} | {item.woodSpecies}
                          </div>
                        </td>
                        <td className="py-4 px-4 text-slate-700 whitespace-nowrap">
                          {item.finishSpec || '—'}
                        </td>
                        <td className="py-4 px-4 font-bold text-slate-900 whitespace-nowrap">
                          {item.orderedQty} cái
                        </td>
                        <td className="py-4 px-4 whitespace-nowrap">
                          {item.attachedCadFileName ? (
                            <button
                              type="button"
                              onClick={() => setPreviewAttachment(item.attachedCadFileName || null)}
                              className="text-blue-600 hover:text-blue-700 hover:underline font-medium flex items-center gap-1.5 text-sm cursor-pointer"
                            >
                              <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                              </svg>
                              <span>{item.attachedCadFileName}</span>
                            </button>
                          ) : (
                            <span className="text-slate-400 text-xs italic">Chưa đính kèm</span>
                          )}
                        </td>
                        <td className="py-4 px-4 whitespace-nowrap">
                          <span
                            className={`inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full border whitespace-nowrap ${badgeStyle.badge}`}
                          >
                            <span className={`w-1.5 h-1.5 rounded-full ${badgeStyle.dot}`} />
                            {bomStatusLabel(statusText)}
                          </span>
                        </td>
                        <td className="py-4 px-6 text-right whitespace-nowrap">
                          <button
                            type="button"
                            onClick={() =>
                              navigate(
                                `/bom?spaceKey=${encodeURIComponent(activeKey)}&orderItemId=${item.orderItemId}`
                              )
                            }
                            className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 cursor-pointer"
                          >
                            Xem BOM →
                          </button>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* Recent Space Activity & Milestones */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
          <h2 className="text-base font-bold text-slate-900 mb-4">
            Nhật ký tiến độ &amp; Cột mốc đơn hàng
          </h2>

          {milestones.length === 0 ? (
            <p className="text-sm text-slate-500">Chưa có cột mốc hoạt động nào được ghi nhận.</p>
          ) : (
            <div className="relative pl-6 border-l-2 border-slate-200 space-y-5 text-sm">
              {milestones.map((m, i) => (
                <div key={i} className="relative">
                  <span
                    className={`w-3 h-3 rounded-full absolute -left-[31px] top-1 ring-4 ring-white ${
                      i === 0 ? 'bg-emerald-500' : i === 1 ? 'bg-blue-600' : 'bg-slate-400'
                    }`}
                  />
                  <div className="text-slate-900">
                    <strong className="font-bold">{m.title}</strong>{' '}
                    <span className="text-slate-500 text-xs">
                      — {new Date(m.timestamp).toLocaleString('vi-VN')} bởi {m.actorName} ({m.actorRole})
                    </span>
                  </div>
                  <p className="text-slate-600 text-sm mt-0.5">{m.description}</p>
                </div>
              ))}
            </div>
          )}
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
