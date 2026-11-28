import { Fragment, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { TransformWrapper, TransformComponent } from 'react-zoom-pan-pinch';
import { ApiError } from '../../../lib/api/apiError';
import { HeaderTopLeft } from '../../../components/layout/WmmsAppShell';
import { resolveBackendAssetUrl } from '../../../constants/env';
import { useWmms } from '../../auth/hooks/WmmsContext';
import { useSession } from '../../auth/store/SessionContext';
import { bomStatusLabel, lifecycleStatusLabel } from '../../../lib/statusLabels';
import { productionApi } from '../../production/api/productionApi';
import { useOrderSpaceDetail } from '../hooks/useOrderSpaceDetail';
import type { OrderSpaceLineItemForBom } from '../../bom/types';

const fmtNum = (n?: number | null) => (n ?? 0).toLocaleString('vi-VN');

/** Nhận diện URL ảnh hợp lệ của dòng sản phẩm và chuẩn hóa theo origin Backend */
const getLineItemImageUrl = (item: OrderSpaceLineItemForBom): string | null => {
  const candidates = [item.productImageUrl, item.attachedCadUrl];
  for (const candidate of candidates) {
    if (typeof candidate === 'string' && candidate.trim().length > 0) {
      const trimmed = candidate.trim();
      if (
        trimmed.startsWith('data:image/') ||
        trimmed.startsWith('http://') ||
        trimmed.startsWith('https://') ||
        trimmed.startsWith('/uploads/') ||
        trimmed.startsWith('blob:') ||
        /\.(png|jpe?g|webp|gif|svg)$/i.test(trimmed)
      ) {
        return resolveBackendAssetUrl(trimmed);
      }
    }
  }
  return null;
};

/**
 * Màn hình III.4.3: Order Space Detail Dashboard (SCR-ORD-03)
 */
export default function OrderSpaceDetailPage() {
  const { spaceKey } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { orderSpaces, currentSpaceKey } = useWmms();
  const { currentUser } = useSession();

  const [hoverPreview, setHoverPreview] = useState<{
    visible: boolean;
    x: number;
    y: number;
    placement: 'top' | 'bottom';
    imageUrl: string;
    title: string;
  } | null>(null);

  const [modalPreview, setModalPreview] = useState<{
    isOpen: boolean;
    imageUrl: string;
    title: string;
    sku: string;
    desc: string;
  } | null>(null);

  const [zoomScale, setZoomScale] = useState<number>(1);
  const [brokenImageIds, setBrokenImageIds] = useState<Record<number, boolean>>({});
  const [textAttachmentModal, setTextAttachmentModal] = useState<{
    fileName: string;
    url?: string | null;
  } | null>(null);

  const activeKey = spaceKey || currentSpaceKey || orderSpaces[0]?.spaceKey || '';

  // Quyền người dùng (SALES chỉ xem thông tin đơn hàng & tiến độ, không truy cập BOM hay Đối chiếu tồn kho MRP)
  const activeRole = currentUser?.activeRole || 'OWNER';
  const canReleaseBatches = ['OWNER', 'ADMIN', 'LEADER'].includes(activeRole);
  const canAccessBom = ['OWNER', 'PLANNER', 'ADMIN'].includes(activeRole);
  const canAccessReconciliation = [
    'OWNER',
    'PLANNER',
    'PURCHASING',
    'WAREHOUSE',
    'ADMIN',
  ].includes(activeRole);
  const canAccessProductionProgress = [
    'ADMIN',
    'OWNER',
    'PLANNER',
    'LEADER',
    'QC',
    'WAREHOUSE',
  ].includes(activeRole);

  // Dữ liệu Không gian đơn hàng & Thẻ lô từ TanStack Query (caching 5 phút)
  const { spaceDetail, batches, isLoading, isFetching, isError } = useOrderSpaceDetail(
    activeKey,
    canAccessProductionProgress,
  );

  // Phân trang danh sách sản phẩm (Pagination state giống UserManagementPage)
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);

  // Modal Phát hành Lệnh sản xuất Hàng loạt (Bulk Release Modal)
  const [isBulkModalOpen, setIsBulkModalOpen] = useState<boolean>(false);
  const [selectedBulkItemIds, setSelectedBulkItemIds] = useState<number[]>([]);
  const [bulkSplitMode, setBulkSplitMode] = useState<'MERGE_SINGLE' | 'UNIFORM_SIZE'>('MERGE_SINGLE');
  const [bulkUniformBatchSize, setBulkUniformBatchSize] = useState<number>(100);
  const [bulkAllowOverdrive, setBulkAllowOverdrive] = useState<boolean>(false);
  const [bulkOverdriveJustification, setBulkOverdriveJustification] = useState<string>('');
  const [isBulkExecuting, setIsBulkExecuting] = useState<boolean>(false);
  const [bulkProgress, setBulkProgress] = useState<{
    current: number;
    total: number;
    currentProductName: string;
  } | null>(null);

  const fallbackSpace = orderSpaces.find((s) => s.spaceKey === activeKey);
  const lineItems = spaceDetail?.lineItems || [];
  const milestones = spaceDetail?.milestones || [];

  // Tính toán số lượng đã phát hành và còn lại cho từng sản phẩm
  const getItemStats = (item: OrderSpaceLineItemForBom) => {
    const itemBatches = batches.filter(
      (b) => b.orderItemId === item.orderItemId && b.batchType === 'MAIN_CARD',
    );
    const initialReleasedQty = itemBatches.reduce((sum, b) => {
      const firstTarget = b.steps?.[0]?.targetQty;
      return sum + (firstTarget && firstTarget > b.batchQty ? firstTarget : b.batchQty);
    }, 0);
    const remainingQty = Math.max(0, item.orderedQty - initialReleasedQty);

    // Kiểm tra điều kiện BOM: phải có BOM header/ID và không ở trạng thái PENDING_BOM / NOT_CREATED
    const hasBom =
      Boolean(item.bomId) &&
      item.bomLifecycleBadge !== 'PENDING_BOM' &&
      item.bomLifecycleBadge !== 'NOT_CREATED';

    const isBomApproved =
      item.bomLifecycleBadge === 'READY_FOR_PRODUCTION' ||
      item.bomLifecycleBadge === 'APPROVED';

    // Đánh giá tính hợp lệ để tick chọn phát hành hàng loạt:
    // 1. Chưa có BOM -> Chặn cứng (Disabled), nộp lên backend chắc chắn ném BOM_NOT_CONFIGURED
    // 2. Đã phát hành đủ số lượng -> Chặn (Disabled)
    let isEligible = true;
    let disableReason: string | null = null;

    if (!hasBom) {
      isEligible = false;
      disableReason = 'Chưa thiết lập định mức BOM (Chặn cứng)';
    } else if (remainingQty <= 0) {
      isEligible = false;
      disableReason = 'Đã phát hành đủ số lượng';
    }

    return {
      initialReleasedQty,
      remainingQty,
      hasBom,
      isBomApproved,
      isEligible,
      disableReason,
      batchesCount: itemBatches.length,
    };
  };

  // Danh sách các mục đủ điều kiện để phát hành hàng loạt
  const eligibleBulkItems = useMemo(() => {
    return lineItems.filter((item) => getItemStats(item).isEligible);
  }, [lineItems, batches]);

  const isAllEligibleSelected = useMemo(() => {
    if (eligibleBulkItems.length === 0) return false;
    return eligibleBulkItems.every((item) => selectedBulkItemIds.includes(item.orderItemId));
  }, [eligibleBulkItems, selectedBulkItemIds]);

  const handleToggleSelectAll = () => {
    if (isAllEligibleSelected) {
      setSelectedBulkItemIds([]);
    } else {
      setSelectedBulkItemIds(eligibleBulkItems.map((item) => item.orderItemId));
    }
  };

  const handleToggleSelectItem = (orderItemId: number) => {
    setSelectedBulkItemIds((prev) =>
      prev.includes(orderItemId) ? prev.filter((id) => id !== orderItemId) : [...prev, orderItemId],
    );
  };

  const handleOpenBulkModal = () => {
    // Mặc định chọn tất cả sản phẩm đủ điều kiện
    setSelectedBulkItemIds(eligibleBulkItems.map((item) => item.orderItemId));
    setBulkSplitMode('MERGE_SINGLE');
    setBulkUniformBatchSize(100);
    setBulkAllowOverdrive(false);
    setBulkOverdriveJustification(
      'Kích hoạt chạy trước sơ chế phôi hàng loạt cho các dòng sản phẩm trong Không gian đơn hàng.',
    );
    setIsBulkModalOpen(true);
  };

  // Submit Phát hành Lệnh sản xuất Hàng loạt
  const handleSubmitBulkGenerate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedBulkItemIds.length === 0) return;

    if (bulkAllowOverdrive && !bulkOverdriveJustification.trim()) {
      toast.error('Khi kích hoạt chế độ chạy trước Overdrive, bắt buộc phải nhập lý do giải trình.');
      return;
    }

    const itemsToProcess = lineItems.filter((item) =>
      selectedBulkItemIds.includes(item.orderItemId),
    );

    setIsBulkExecuting(true);
    let successCount = 0;
    const errors: string[] = [];

    for (let i = 0; i < itemsToProcess.length; i++) {
      const item = itemsToProcess[i];
      const stats = getItemStats(item);
      const rem = stats.remainingQty;

      setBulkProgress({
        current: i + 1,
        total: itemsToProcess.length,
        currentProductName: item.productName,
      });

      if (!stats.hasBom) {
        errors.push(`SP "${item.productName}": Chưa có cấu hình BOM.`);
        continue;
      }

      if (rem <= 0) {
        continue;
      }

      const maxBatchSize =
        bulkSplitMode === 'MERGE_SINGLE'
          ? rem
          : Math.max(1, bulkUniformBatchSize || rem);

      try {
        await productionApi.generateBatches({
          orderItemId: item.orderItemId,
          maxBatchSize,
          allowOverdrive: bulkAllowOverdrive,
          justification: bulkAllowOverdrive
            ? bulkOverdriveJustification.trim()
            : `Phát hành hàng loạt theo định mức ${maxBatchSize} SP/lô.`,
        });
        successCount++;
      } catch (err) {
        let msg = `Lỗi SP "${item.productName}": `;
        if (err instanceof ApiError) {
          msg += err.message;
        } else if (err instanceof Error) {
          msg += err.message;
        } else {
          msg += 'Không thể phát hành.';
        }
        errors.push(msg);
      }
    }

    setIsBulkExecuting(false);
    setBulkProgress(null);
    setIsBulkModalOpen(false);
    setSelectedBulkItemIds([]);

    // Tự động làm mới cache TanStack Query
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['order-space', activeKey] }),
      queryClient.invalidateQueries({ queryKey: ['order-space-batches', activeKey] }),
    ]);

    if (errors.length === 0) {
      toast.success(`Đã phát hành lệnh sản xuất hàng loạt thành công cho ${successCount} sản phẩm!`);
    } else {
      toast.error(
        `Hoàn tất với cảnh báo: ${successCount} thành công, ${errors.length} thất bại. (${errors.join('; ')})`,
      );
    }
  };

  // Tính toán phân trang
  const totalCount = lineItems.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const startItem = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endItem = Math.min(pageNumber * pageSize, totalCount);

  const pagedLineItems = useMemo(() => {
    const start = (pageNumber - 1) * pageSize;
    return lineItems.slice(start, start + pageSize);
  }, [lineItems, pageNumber, pageSize]);

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
    <>
      <HeaderTopLeft>
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
      </HeaderTopLeft>
      <div className="max-w-[1400px] mx-auto space-y-6">
        {isError && (
          <div className="px-4 py-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-800 text-sm font-medium">
            Không thể tải dữ liệu Không gian đơn hàng &ldquo;{activeKey}&rdquo;. Vui lòng thử lại.
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
                {(spaceDetail as { lifecycleStatusLabel?: string })?.lifecycleStatusLabel ||
                  lifecycleStatusLabel(
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
                <strong
                  className={
                    (spaceDetail?.isUrgentEtd ?? fallbackSpace?.isUrgentEtd) ||
                    (spaceDetail?.daysRemaining ?? fallbackSpace?.daysRemaining ?? 0) < 0
                      ? 'text-rose-600'
                      : 'text-slate-900'
                  }
                >
                  {spaceDetail?.etdDate
                    ? new Date(spaceDetail.etdDate).toLocaleDateString('vi-VN')
                    : fallbackSpace?.etdDate || '—'}{' '}
                  ({spaceDetail?.daysRemaining ?? fallbackSpace?.daysRemaining ?? 0} ngày)
                </strong>
              </span>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2.5 shrink-0">
            {/* Nút Phát hành Lệnh sản xuất Hàng loạt */}
            {canReleaseBatches && (
              <button
                type="button"
                onClick={handleOpenBulkModal}
                className="px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-bold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
              >
                <span>Phát hành Lệnh SX hàng loạt</span>
                {eligibleBulkItems.length > 0 && (
                  <span className="px-1.5 py-0.5 bg-blue-800 text-white rounded-full text-xs">
                    {eligibleBulkItems.length}
                  </span>
                )}
              </button>
            )}

            {canAccessReconciliation && (
              <button
                type="button"
                onClick={() => navigate(`/inventory/reconciliation?spaceKey=${encodeURIComponent(activeKey)}`)}
                className="px-4 py-2.5 bg-slate-800 hover:bg-slate-900 text-white text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4" />
                </svg>
                <span>Đối chiếu tồn kho &amp; Khóa vật tư →</span>
              </button>
            )}
          </div>
        </div>

        {/* 4 KPI Cards (Ẩn đối với vai trò SALES) */}
        {activeRole !== 'SALES' && (
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
              <div
                className={`text-2xl font-extrabold mt-2 ${
                  spaceDetail?.bomStatusTitle === 'Sẵn sàng' ||
                  (spaceDetail?.bomStatusTitle || '').includes('Sẵn sàng')
                    ? 'text-emerald-600'
                    : 'text-amber-600'
                }`}
              >
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
        )}

        {/* Recent Space Activity & Milestones (Đặt ngay dưới khung thông tin đơn hàng) */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
          <h2 className="text-base font-bold text-slate-900 mb-4">
            Nhật ký tiến độ &amp; Cột mốc đơn hàng
          </h2>

          {milestones.length === 0 ? (
            <p className="text-sm text-slate-500">Chưa có cột mốc hoạt động nào được ghi nhận.</p>
          ) : (
            <div className="relative pl-6 border-l-2 border-slate-200 space-y-5 text-sm">
              {milestones.map((m, i) => {
                const parsedDate = m.timestamp ? new Date(m.timestamp) : null;
                const timeStr =
                  parsedDate && !Number.isNaN(parsedDate.getTime())
                    ? parsedDate.toLocaleString('vi-VN')
                    : m.timestamp || '';
                const actorStr = m.actorName
                  ? `bởi ${m.actorName}${m.actorRole ? ` (${m.actorRole})` : ''}`
                  : '';
                const metaSuffix = [timeStr, actorStr].filter(Boolean).join(' ');

                return (
                  <div key={i} className="relative">
                    <span
                      className={`w-3 h-3 rounded-full absolute -left-[31px] top-1 ring-4 ring-white ${
                        i === 0 ? 'bg-emerald-500' : i === 1 ? 'bg-blue-600' : 'bg-slate-400'
                      }`}
                    />
                    <div className="text-slate-900">
                      <strong className="font-bold">{m.title}</strong>
                      {metaSuffix && (
                        <span className="text-slate-500 text-xs"> — {metaSuffix}</span>
                      )}
                    </div>
                    <p className="text-slate-600 text-sm mt-0.5">{m.description}</p>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Ordered Product Line Items Table */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-white">
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-base font-bold text-slate-900">
                  Danh sách sản phẩm đặt hàng trong Space ({lineItems.length} sản phẩm)
                </h2>
                {isFetching && (
                  <span className="text-xs font-semibold text-blue-600 animate-pulse">
                    Đang đồng bộ...
                  </span>
                )}
              </div>
            </div>
          </div>

          <div className="overflow-x-auto bg-white">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-4 min-w-[95px]">Mã SKU</th>
                  <th className="py-3.5 px-3 min-w-[190px]">Mẫu sản phẩm &amp; quy cách</th>
                  <th className="py-3.5 px-3 min-w-[115px]">Quy cách hoàn thiện</th>
                  <th className="py-3.5 px-3 min-w-[90px]">Số lượng đặt</th>
                  <th className="py-3.5 px-3 min-w-[130px]">Tiến độ phát hành</th>
                  <th className="py-3.5 px-3 min-w-[145px]">Bản vẽ kỹ thuật</th>
                  <th className="py-3.5 px-3 min-w-[140px]">Trạng thái BOM</th>
                  <th className="py-3.5 px-4 text-right min-w-[175px]">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm bg-white">
                {isLoading ? (
                  <tr>
                    <td colSpan={8} className="py-10 px-6 text-center text-slate-500">
                      Đang tải danh sách sản phẩm trong Không gian đơn hàng...
                    </td>
                  </tr>
                ) : lineItems.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="py-10 px-6 text-center text-rose-600 font-semibold">
                      Lỗi: Không tìm thấy dữ liệu sản phẩm nào trong Không gian đơn hàng này.
                    </td>
                  </tr>
                ) : (
                  pagedLineItems.map((item) => {
                    const statusText = item.bomLifecycleBadge || item.itemStatus || 'PENDING';
                    const badgeStyle = getBomBadgeStyle(statusText);
                    const stats = getItemStats(item);

                    return (
                      <tr key={item.orderItemId} className="hover:bg-slate-50/90 transition-colors">
                        <td className="py-4 px-4 font-mono font-bold text-slate-800 whitespace-nowrap">
                          {item.productSku}
                          <div className="text-[11px] text-slate-400 font-normal">
                            #{item.orderItemId}
                          </div>
                        </td>
                        <td className="py-4 px-3">
                          <div className="font-semibold text-slate-900">{item.productName}</div>
                          <div className="text-xs text-slate-500 mt-0.5">
                            {item.dimensionsSpec} | {item.woodSpecies}
                          </div>
                        </td>
                        <td className="py-4 px-3 text-slate-700 whitespace-nowrap">
                          {item.finishSpec || '—'}
                        </td>
                        <td className="py-4 px-3 font-bold text-slate-900 whitespace-nowrap">
                          {fmtNum(item.orderedQty)} cái
                        </td>
                        <td className="py-4 px-3 whitespace-nowrap">
                          <div className="text-xs font-bold text-slate-800">
                            <span className={stats.initialReleasedQty > 0 ? 'text-blue-600' : 'text-slate-400'}>
                              {fmtNum(stats.initialReleasedQty)}
                            </span>{' '}
                            / {fmtNum(item.orderedQty)} SP
                          </div>
                          <div className="text-[11px] text-slate-500 mt-0.5">
                            {stats.remainingQty > 0 ? (
                              <span>Còn {fmtNum(stats.remainingQty)} SP</span>
                            ) : (
                              <span className="text-emerald-600 font-bold">✓ Đã phát hành đủ</span>
                            )}
                          </div>
                        </td>
                        <td className="py-4 px-3 whitespace-nowrap">
                          {(() => {
                            const imgUrl = !brokenImageIds[item.orderItemId]
                              ? getLineItemImageUrl(item)
                              : null;
                            const drawingTitle = item.attachedCadFileName || `Ảnh ${item.productSku}`;

                            if (imgUrl) {
                              return (
                                <button
                                  type="button"
                                  onClick={() => {
                                    setHoverPreview(null);
                                    setZoomScale(1);
                                    setModalPreview({
                                      isOpen: true,
                                      imageUrl: imgUrl,
                                      title: drawingTitle,
                                      sku: item.productSku,
                                      desc: item.productName,
                                    });
                                  }}
                                  onMouseEnter={(e) => {
                                    const rect = e.currentTarget.getBoundingClientRect();
                                    const isTop = rect.top > 240;
                                    setHoverPreview({
                                      visible: true,
                                      x: Math.max(10, Math.min(rect.left, window.innerWidth - 240)),
                                      y: isTop ? rect.top - 8 : rect.bottom + 8,
                                      placement: isTop ? 'top' : 'bottom',
                                      imageUrl: imgUrl,
                                      title: drawingTitle,
                                    });
                                  }}
                                  onMouseLeave={() => setHoverPreview(null)}
                                  className="inline-flex items-center gap-2 bg-slate-50 border border-slate-200 hover:border-blue-400 rounded-lg p-1 pr-2.5 transition cursor-pointer max-w-[135px] min-w-0 overflow-hidden text-left"
                                  title={`${drawingTitle} — Nhấn để phóng to ảnh bản vẽ`}
                                >
                                  <img
                                    src={imgUrl}
                                    alt={drawingTitle}
                                    onError={() =>
                                      setBrokenImageIds((prev) => ({
                                        ...prev,
                                        [item.orderItemId]: true,
                                      }))
                                    }
                                    className="w-7 h-7 rounded object-cover border border-slate-200 shrink-0 bg-white"
                                  />
                                  <span className="text-xs font-mono font-medium text-slate-700 truncate min-w-0 flex-1">
                                    {drawingTitle}
                                  </span>
                                </button>
                              );
                            }

                            if (item.attachedCadFileName) {
                              return (
                                <button
                                  type="button"
                                  onClick={() =>
                                    setTextAttachmentModal({
                                      fileName: item.attachedCadFileName || '',
                                      url: resolveBackendAssetUrl(item.attachedCadUrl),
                                    })
                                  }
                                  title={item.attachedCadFileName}
                                  className="text-blue-600 hover:text-blue-700 hover:underline font-medium inline-flex items-center gap-1.5 text-xs cursor-pointer max-w-[135px] min-w-0"
                                >
                                  <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                                  </svg>
                                  <span className="truncate min-w-0">{item.attachedCadFileName}</span>
                                </button>
                              );
                            }

                            return <span className="text-slate-400 text-xs italic">Chưa đính kèm</span>;
                          })()}
                        </td>
                        <td className="py-4 px-3 whitespace-nowrap">
                          <span
                            className={`inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full border whitespace-nowrap ${badgeStyle.badge}`}
                          >
                            <span className={`w-1.5 h-1.5 rounded-full ${badgeStyle.dot}`} />
                            {(item as { bomLifecycleLabel?: string; itemStatusLabel?: string }).bomLifecycleLabel ||
                              (item as { itemStatusLabel?: string }).itemStatusLabel ||
                              bomStatusLabel(statusText)}
                          </span>
                        </td>
                        <td className="py-4 px-4 text-right whitespace-nowrap space-x-2">
                          {canAccessBom && (
                            <button
                              type="button"
                              onClick={() =>
                                navigate(
                                  `/bom?spaceKey=${encodeURIComponent(activeKey)}&orderItemId=${item.orderItemId}&sku=${encodeURIComponent(item.productSku)}`
                                )
                              }
                              className="px-2.5 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 cursor-pointer"
                            >
                              Xem BOM →
                            </button>
                          )}

                          {canAccessProductionProgress && (
                            <button
                              type="button"
                              onClick={() =>
                                navigate(
                                  `/production/progress?spaceKey=${encodeURIComponent(activeKey)}&orderItemId=${item.orderItemId}&sku=${encodeURIComponent(item.productSku)}`
                                )
                              }
                              className="px-2.5 py-1.5 text-xs font-bold text-blue-700 bg-blue-50 border border-blue-200 rounded-lg hover:bg-blue-100 transition cursor-pointer shadow-2xs"
                            >
                              Tiến trình SX →
                            </button>
                          )}

                          {!canAccessBom && !canAccessProductionProgress && (
                            <span className="text-xs text-slate-400 italic">Chỉ xem</span>
                          )}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer (TanStack cached pagination) */}
          <div className="px-6 py-4 border-t border-slate-200 bg-[#F8FAFC] flex flex-col sm:flex-row sm:items-center justify-between gap-4 text-sm text-slate-600">
            <div className="flex flex-wrap items-center gap-3">
              <span>
                Hiển thị <strong className="text-slate-900">{startItem}</strong> đến{' '}
                <strong className="text-slate-900">{endItem}</strong> trong tổng số{' '}
                <strong className="text-slate-900">{totalCount}</strong> sản phẩm
              </span>

              <div className="flex items-center gap-2 sm:ml-2 sm:border-l sm:border-slate-200 sm:pl-3">
                <span className="text-slate-500 text-xs">Số dòng:</span>
                <select
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setPageNumber(1);
                  }}
                  aria-label="Số dòng mỗi trang"
                  className="px-2.5 py-1 bg-white border border-slate-300 rounded-lg text-xs font-semibold text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-600"
                >
                  <option value={5}>5</option>
                  <option value={10}>10</option>
                  <option value={20}>20</option>
                  <option value={50}>50</option>
                </select>
              </div>
            </div>

            <div className="flex items-center gap-1.5">
              <button
                type="button"
                disabled={pageNumber <= 1 || isFetching}
                onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition cursor-pointer"
              >
                Trước
              </button>

              <div className="flex items-center gap-1 px-1">
                {Array.from({ length: totalPages }, (_, i) => i + 1)
                  .filter(
                    (p) =>
                      p === 1 ||
                      p === totalPages ||
                      Math.abs(p - pageNumber) <= 1,
                  )
                  .map((p, idx, arr) => (
                    <Fragment key={p}>
                      {idx > 0 && arr[idx - 1] !== p - 1 && (
                        <span className="px-1 text-slate-400 text-xs">...</span>
                      )}
                      <button
                        type="button"
                        disabled={isFetching}
                        onClick={() => setPageNumber(p)}
                        className={`w-8 h-8 text-xs font-bold rounded-lg transition cursor-pointer ${
                          p === pageNumber
                            ? 'bg-blue-600 text-white shadow-xs'
                            : 'text-slate-700 bg-white border border-slate-300 hover:bg-slate-50'
                        }`}
                      >
                        {p}
                      </button>
                    </Fragment>
                  ))}
              </div>

              <button
                type="button"
                disabled={pageNumber >= totalPages || isFetching}
                onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
                className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition cursor-pointer"
              >
                Sau
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* MODAL PHÁT HÀNH LỆNH SẢN XUẤT HÀNG LOẠT (BULK RELEASE) */}
      {canReleaseBatches && isBulkModalOpen && (
        <div className="fixed inset-0 z-50 bg-slate-900/60 flex items-center justify-center p-4 overflow-y-auto">
          <div className="bg-white rounded-2xl max-w-3xl w-full p-6 shadow-2xl space-y-4 my-8">
            <div className="flex items-center justify-between border-b border-slate-200 pb-3">
              <div>
                <h3 className="text-base font-bold text-slate-900">
                  Phát hành Lệnh sản xuất Hàng loạt ({selectedBulkItemIds.length}/{eligibleBulkItems.length} sản phẩm)
                </h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Không gian đơn hàng: <strong className="font-mono text-slate-800">{activeKey}</strong>.
                  Các sản phẩm chưa có định mức BOM hoặc đã phát hành đủ số lượng sẽ được khóa an toàn.
                </p>
              </div>
              {!isBulkExecuting && (
                <button
                  type="button"
                  onClick={() => setIsBulkModalOpen(false)}
                  className="text-slate-400 hover:text-slate-600 text-lg font-bold cursor-pointer"
                >
                  ×
                </button>
              )}
            </div>

            <form onSubmit={handleSubmitBulkGenerate} className="space-y-4 text-sm">
              {/* Bảng xem trước & tick chọn sản phẩm */}
              <div className="border border-slate-200 rounded-xl overflow-hidden">
                <div className="max-h-56 overflow-y-auto">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-slate-50 border-b border-slate-200 font-bold text-slate-600 uppercase sticky top-0 z-10">
                      <tr>
                        <th className="px-3 py-2 text-center w-10">
                          <input
                            type="checkbox"
                            checked={isAllEligibleSelected}
                            onChange={handleToggleSelectAll}
                            disabled={eligibleBulkItems.length === 0}
                            className="rounded border-slate-300 text-blue-600 focus:ring-blue-500 cursor-pointer disabled:cursor-not-allowed"
                            title="Chọn tất cả sản phẩm đủ điều kiện"
                          />
                        </th>
                        <th className="px-3 py-2">Mã SKU &amp; Tên sản phẩm</th>
                        <th className="px-3 py-2">Trạng thái BOM</th>
                        <th className="px-3 py-2 text-right">SL Đặt</th>
                        <th className="px-3 py-2 text-right">Còn lại cần phát hành</th>
                        <th className="px-3 py-2 text-center">Dự kiến chia lô</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {lineItems.map((item) => {
                        const stats = getItemStats(item);
                        const isChecked = selectedBulkItemIds.includes(item.orderItemId);
                        const estimatedBatchesCount =
                          bulkSplitMode === 'MERGE_SINGLE'
                            ? 1
                            : Math.ceil(stats.remainingQty / (bulkUniformBatchSize || stats.remainingQty || 1));

                        return (
                          <tr
                            key={item.orderItemId}
                            className={`transition ${
                              !stats.isEligible
                                ? 'bg-slate-50/70 opacity-60'
                                : isChecked
                                ? 'bg-blue-50/60'
                                : 'hover:bg-slate-50'
                            }`}
                          >
                            <td className="px-3 py-2.5 text-center">
                              <input
                                type="checkbox"
                                checked={isChecked}
                                disabled={!stats.isEligible}
                                onChange={() => handleToggleSelectItem(item.orderItemId)}
                                className="rounded border-slate-300 text-blue-600 focus:ring-blue-500 cursor-pointer disabled:cursor-not-allowed"
                                title={stats.disableReason || 'Chọn sản phẩm này'}
                              />
                            </td>
                            <td className="px-3 py-2.5">
                              <div className="font-semibold text-slate-900">{item.productName}</div>
                              <div className="text-[11px] text-slate-500 font-mono">
                                {item.productSku} (#{item.orderItemId})
                              </div>
                              {stats.disableReason && (
                                <div className="text-[10px] text-rose-600 font-bold mt-0.5">
                                  ⚠ {stats.disableReason}
                                </div>
                              )}
                            </td>
                            <td className="px-3 py-2.5 whitespace-nowrap">
                              {stats.hasBom ? (
                                <span
                                  className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold ${
                                    stats.isBomApproved
                                      ? 'bg-emerald-100 text-emerald-800'
                                      : 'bg-amber-100 text-amber-800'
                                  }`}
                                >
                                  {stats.isBomApproved ? '✓ BOM Đã duyệt' : 'BOM Chờ duyệt'}
                                </span>
                              ) : (
                                <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold bg-rose-100 text-rose-800">
                                  ✕ Chưa có BOM
                                </span>
                              )}
                            </td>
                            <td className="px-3 py-2.5 text-right font-medium text-slate-700">
                              {fmtNum(item.orderedQty)}
                            </td>
                            <td className="px-3 py-2.5 text-right font-bold text-slate-900 whitespace-nowrap">
                              {fmtNum(stats.remainingQty)} SP
                            </td>
                            <td className="px-3 py-2.5 text-center font-bold text-blue-700 whitespace-nowrap">
                              {!stats.isEligible ? (
                                <span className="text-slate-400 font-normal italic">—</span>
                              ) : bulkSplitMode === 'MERGE_SINGLE' ? (
                                <span>1 lô ({fmtNum(stats.remainingQty)} SP)</span>
                              ) : (
                                <span>
                                  {estimatedBatchesCount} lô (≤ {fmtNum(bulkUniformBatchSize)} SP/lô)
                                </span>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Chọn chế độ chia lô */}
              <div className="space-y-3 p-4 bg-slate-50 border border-slate-200 rounded-xl">
                <label className="block text-xs font-bold text-slate-700">
                  Phương thức phân bổ kích thước lô (Batch Size Strategy):
                </label>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <label
                    className={`p-3.5 rounded-xl border cursor-pointer transition flex items-start gap-2.5 ${
                      bulkSplitMode === 'MERGE_SINGLE'
                        ? 'bg-blue-50 border-blue-400 ring-1 ring-blue-300'
                        : 'bg-white border-slate-200 hover:border-slate-300'
                    }`}
                  >
                    <input
                      type="radio"
                      name="bulkSplitMode"
                      value="MERGE_SINGLE"
                      checked={bulkSplitMode === 'MERGE_SINGLE'}
                      onChange={() => setBulkSplitMode('MERGE_SINGLE')}
                      className="mt-0.5 text-blue-600 focus:ring-blue-500"
                    />
                    <div className="text-xs">
                      <strong className="text-slate-900 block font-bold">
                        Gộp 1 lô duy nhất cho mỗi SP
                      </strong>
                      <span className="text-slate-500 mt-0.5 block leading-relaxed">
                        Mỗi sản phẩm phát hành 1 thẻ lô bằng đúng số lượng còn lại chưa phát hành.
                      </span>
                    </div>
                  </label>

                  <label
                    className={`p-3.5 rounded-xl border cursor-pointer transition flex items-start gap-2.5 ${
                      bulkSplitMode === 'UNIFORM_SIZE'
                        ? 'bg-blue-50 border-blue-400 ring-1 ring-blue-300'
                        : 'bg-white border-slate-200 hover:border-slate-300'
                    }`}
                  >
                    <input
                      type="radio"
                      name="bulkSplitMode"
                      value="UNIFORM_SIZE"
                      checked={bulkSplitMode === 'UNIFORM_SIZE'}
                      onChange={() => setBulkSplitMode('UNIFORM_SIZE')}
                      className="mt-0.5 text-blue-600 focus:ring-blue-500"
                    />
                    <div className="text-xs">
                      <strong className="text-slate-900 block font-bold">
                        Áp dụng định mức BatchSize chung
                      </strong>
                      <span className="text-slate-500 mt-0.5 block leading-relaxed">
                        Tất cả sản phẩm chia thành các lô đều nhau theo công suất trạm tối đa.
                      </span>
                    </div>
                  </label>
                </div>

                {bulkSplitMode === 'UNIFORM_SIZE' && (
                  <div className="mt-3 pt-3 border-t border-slate-200 space-y-2">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <label className="text-xs font-bold text-slate-700">
                        Kích thước tối đa mỗi lô (BatchSize đồng bộ):
                      </label>
                      <div className="flex flex-wrap gap-1.5">
                        {[50, 100, 200, 250, 500].map((preset) => (
                          <button
                            key={preset}
                            type="button"
                            onClick={() => setBulkUniformBatchSize(preset)}
                            className="px-2.5 py-1 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 rounded text-[11px] font-semibold cursor-pointer"
                          >
                            {preset} SP/lô
                          </button>
                        ))}
                      </div>
                    </div>
                    <input
                      type="number"
                      min={1}
                      max={10000}
                      value={bulkUniformBatchSize}
                      onChange={(e) => setBulkUniformBatchSize(Number(e.target.value))}
                      className="w-full px-3 py-2 border border-slate-300 bg-white rounded-lg font-bold text-slate-900 text-sm"
                      required
                    />
                  </div>
                )}
              </div>

              {/* Tùy chọn Overdrive */}
              <div className="p-4 rounded-xl border border-amber-200 bg-amber-50/60 space-y-2.5">
                <label className="flex items-start gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={bulkAllowOverdrive}
                    onChange={(e) => setBulkAllowOverdrive(e.target.checked)}
                    className="mt-0.5 w-4 h-4 rounded border-amber-400 text-amber-600"
                  />
                  <div className="text-xs">
                    <span className="font-bold text-amber-900">
                      Kích hoạt chế độ Chạy trước (Overdrive) hàng loạt
                    </span>
                    <p className="text-slate-600 mt-0.5">
                      Cho phép các lô sản xuất bắt đầu chạy ngay công đoạn sơ chế trước điểm dừng Hold Point kể cả khi chưa hoàn tất xuất kho vật tư.
                    </p>
                  </div>
                </label>

                {bulkAllowOverdrive && (
                  <div>
                    <label className="block text-xs font-bold text-slate-700 mb-1">
                      Lý do giải trình kỹ thuật (bắt buộc):
                    </label>
                    <textarea
                      rows={2}
                      maxLength={500}
                      value={bulkOverdriveJustification}
                      onChange={(e) => setBulkOverdriveJustification(e.target.value)}
                      className="w-full px-3 py-2 border border-amber-300 bg-white rounded-lg text-xs"
                      placeholder="Nhập lý do chạy trước hàng loạt (tối đa 500 ký tự)..."
                      required
                    />
                  </div>
                )}
              </div>

              {/* Thanh tiến trình thực thi hàng loạt */}
              {isBulkExecuting && bulkProgress && (
                <div className="p-4 bg-blue-50 border border-blue-200 rounded-xl space-y-2">
                  <div className="flex items-center justify-between text-xs font-bold text-blue-900">
                    <span>Đang phát hành sản phẩm: {bulkProgress.currentProductName}...</span>
                    <span>
                      {bulkProgress.current} / {bulkProgress.total} sản phẩm
                    </span>
                  </div>
                  <div className="w-full bg-blue-200 rounded-full h-2 overflow-hidden">
                    <div
                      className="bg-blue-600 h-full rounded-full transition-all duration-300"
                      style={{
                        width: `${Math.round((bulkProgress.current / bulkProgress.total) * 100)}%`,
                      }}
                    />
                  </div>
                </div>
              )}

              <div className="p-3 bg-rose-50 border border-rose-200 rounded-lg text-xs text-rose-800">
                <strong>Lưu ý:</strong> Lệnh sản xuất sau khi phát hành <strong>không thể hủy bỏ</strong>.
                Hệ thống sẽ tạo thẻ lô chính (MAIN_CARD) theo quy trình định tuyến thực tế của từng sản phẩm.
              </div>

              <div className="flex items-center justify-end gap-3 pt-2 border-t border-slate-200">
                <button
                  type="button"
                  disabled={isBulkExecuting}
                  onClick={() => setIsBulkModalOpen(false)}
                  className="px-4 py-2 border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 cursor-pointer text-xs disabled:opacity-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isBulkExecuting || selectedBulkItemIds.length === 0}
                  className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-lg cursor-pointer text-xs disabled:opacity-50 inline-flex items-center gap-2"
                >
                  {isBulkExecuting ? (
                    <>
                      <span className="animate-spin inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full" />
                      <span>Đang phát hành ({bulkProgress?.current}/{bulkProgress?.total})...</span>
                    </>
                  ) : (
                    <span>
                      Xác nhận phát hành ({selectedBulkItemIds.length} sản phẩm)
                    </span>
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Tooltip Hover Preview ảnh bản vẽ */}
      {hoverPreview?.visible && (
        <div
          style={{
            position: 'fixed',
            left: `${hoverPreview.x}px`,
            top: hoverPreview.placement === 'bottom' ? `${hoverPreview.y}px` : undefined,
            bottom: hoverPreview.placement === 'top' ? `${window.innerHeight - hoverPreview.y}px` : undefined,
          }}
          className="z-50 pointer-events-none w-56 bg-white rounded-xl shadow-2xl border border-slate-200 p-2 text-center"
        >
          <div className="w-full h-44 rounded-lg overflow-hidden bg-slate-100 border border-slate-100 flex items-center justify-center">
            <img
              src={hoverPreview.imageUrl}
              alt={hoverPreview.title}
              className="max-w-full max-h-full object-contain"
            />
          </div>
          <div className="mt-1.5 px-1 flex items-center justify-between text-[11px] text-slate-500">
            <span className="truncate max-w-[130px] font-mono text-slate-700 font-semibold">{hoverPreview.title}</span>
            <span className="text-blue-600 font-medium">Click xem lớn</span>
          </div>
        </div>
      )}

      {/* Modal xem lớn ảnh bản vẽ kỹ thuật có hỗ trợ thu phóng và kéo thả (chỉ xem) */}
      {modalPreview?.isOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/70 backdrop-blur-[2px]"
          onClick={() => setModalPreview(null)}
        >
          <div
            className="bg-white rounded-2xl shadow-2xl border border-slate-200 w-[96vw] max-w-6xl h-[88vh] overflow-hidden flex flex-col"
            onClick={(e) => e.stopPropagation()}
          >
            <TransformWrapper
              initialScale={1}
              minScale={0.5}
              maxScale={4}
              centerOnInit={true}
              wheel={{ step: 0.001 }}
              doubleClick={{ step: 0.25 }}
              onTransform={(_ref, state) => setZoomScale(state.scale)}
            >
              {({ zoomIn, zoomOut, resetTransform }) => (
                <>
                  <div className="px-6 py-3 border-b border-slate-200 flex items-center justify-between gap-4 bg-slate-50 shrink-0">
                    <div className="min-w-0 flex-1">
                      <h3 className="text-sm font-bold text-slate-900 flex items-center gap-2 min-w-0">
                        <span className="shrink-0">Bản vẽ kỹ thuật:</span>
                        <span
                          className="font-mono text-blue-600 font-semibold truncate min-w-0"
                          title={modalPreview.title}
                        >
                          {modalPreview.title}
                        </span>
                      </h3>
                      <p
                        className="text-xs text-slate-500 mt-0.5 truncate"
                        title={`Mã SKU: ${modalPreview.sku}${modalPreview.desc ? ` — ${modalPreview.desc}` : ''}`}
                      >
                        Mã SKU: <span className="font-semibold text-slate-700">{modalPreview.sku}</span>
                        {modalPreview.desc ? ` — ${modalPreview.desc}` : ''}
                      </p>
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      <div className="flex items-center bg-white border border-slate-300 rounded-lg shadow-xs overflow-hidden text-xs">
                        <button
                          type="button"
                          onClick={() => zoomOut(0.15)}
                          title="Thu nhỏ"
                          className="w-8 h-8 flex items-center justify-center font-bold text-slate-700 hover:bg-slate-100 transition cursor-pointer select-none text-base"
                        >
                          −
                        </button>
                        <button
                          type="button"
                          onClick={() => {
                            resetTransform();
                            setZoomScale(1);
                          }}
                          title="Bấm để đặt lại 100%"
                          className="px-2.5 h-8 flex items-center justify-center font-mono font-semibold text-slate-700 hover:bg-slate-100 border-x border-slate-200 transition cursor-pointer select-none"
                        >
                          {Math.round(zoomScale * 100)}%
                        </button>
                        <button
                          type="button"
                          onClick={() => zoomIn(0.15)}
                          title="Phóng to"
                          className="w-8 h-8 flex items-center justify-center font-bold text-slate-700 hover:bg-slate-100 transition cursor-pointer select-none text-base"
                        >
                          +
                        </button>
                      </div>

                      <div className="h-6 w-px bg-slate-300 mx-1" />

                      <button
                        type="button"
                        onClick={() => setModalPreview(null)}
                        className="w-8 h-8 flex items-center justify-center text-slate-400 hover:text-slate-700 hover:bg-slate-200 rounded-lg transition cursor-pointer text-sm font-semibold"
                        title="Đóng"
                      >
                        ✕
                      </button>
                    </div>
                  </div>

                  <div className="relative flex-1 bg-zinc-200 overflow-hidden flex items-center justify-center select-none">
                    <TransformComponent
                      wrapperClass="!w-full !h-full flex items-center justify-center cursor-grab active:cursor-grabbing"
                      contentClass="!w-full !h-full flex items-center justify-center"
                    >
                      <img
                        src={modalPreview.imageUrl}
                        alt={modalPreview.title}
                        className="max-w-[85vw] max-h-[70vh] object-contain rounded shadow-lg bg-white"
                        draggable={false}
                      />
                    </TransformComponent>
                  </div>
                </>
              )}
            </TransformWrapper>

            <div className="px-6 py-3 border-t border-slate-200 flex items-center justify-between bg-white shrink-0">
              <span className="text-xs text-slate-500">
                Cuộn chuột hoặc dùng nút +/- để thu phóng, nhấn giữ chuột để kéo xem chi tiết.
              </span>
              <button
                type="button"
                onClick={() => setModalPreview(null)}
                className="px-4 py-2 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg cursor-pointer"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal fallback cho tệp CAD/PDF không phải ảnh */}
      {textAttachmentModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-[1px] flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-white rounded-xl shadow-2xl border border-slate-200 p-6 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-slate-900">
                Bản vẽ kỹ thuật CAD đính kèm
              </h3>
              <button
                type="button"
                onClick={() => setTextAttachmentModal(null)}
                className="text-slate-400 hover:text-slate-600 text-xl font-bold leading-none cursor-pointer"
              >
                ×
              </button>
            </div>
            <p className="text-sm text-slate-700 break-words">
              Tệp: <strong className="font-mono break-all">{textAttachmentModal.fileName}</strong>
            </p>
            <p className="text-xs text-slate-500">
              Tệp này ở định dạng bản vẽ kỹ thuật hoặc chưa có ảnh xem trước trực tiếp.
            </p>
            <div className="flex justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setTextAttachmentModal(null)}
                className="px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg cursor-pointer"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
