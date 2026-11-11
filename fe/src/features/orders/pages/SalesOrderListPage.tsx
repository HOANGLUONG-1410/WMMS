import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import WmmsAppShell from '../../../components/layout/WmmsAppShell';
import { useWmms } from '../../auth/hooks/WmmsContext';
import { lifecycleStatusLabel } from '../../../lib/statusLabels';

/**
 * Màn hình III.4.1: Sales Order List Screen — Sales Orders Directory
 * Tuân thủ 100% thiết kế giao diện và bảng Field Description trong Report 3 (Section III.4.1):
 * - Tiêu đề: `Danh mục đơn hàng bán`
 * - Phụ đề: `Theo dõi tiến độ thực hiện PO của khách, ngày giao dự kiến (ETD) và mở màn hình nhập PO Excel qua Staging.`
 * - Field #1: Nút `+ Nhập PO Excel của khách (Khu Staging)` màu xanh ở góc phải trên cùng.
 * - 3 Thẻ KPI:
 *   + `ĐƠN ĐANG THỰC HIỆN`: `8` (`4 đang sản xuất, 2 đang lập BOM, 2 mới nhận`)
 *   + `URGENT ETD (< 10 DAYS)`: `2` (`Ưu tiên cao trong kế hoạch xưởng`)
 *   + `TỔNG SỐ SẢN PHẨM TRÊN XƯỞNG`: `1,450` (`Ghế ăn, bàn ăn, khung giường`)
 * - Bảng `Official Orders & Production Spaces`:
 *   + Field #2: Dropdown `Order Status Filter` (`Tất cả trạng thái`)
 *   + Field #3: Search Bar `Tìm khách hàng, số PO...`
 *   + Field #4: Bảng dữ liệu với 6 cột: `Mã Order Space`, `Khách hàng / Người mua`, `Mã PO`, `ETD mục tiêu (Giao hàng)`, `Dòng hàng / Số lượng`, `Trạng thái vòng đời`, `Thao tác`
 *   + Field #5: Nút `Mở Space →` mở màn hình `Order Space Detail Dashboard (SCR-ORD-03)`.
 */
export default function SalesOrderListPage() {
  const navigate = useNavigate();
  const { orderSpaces, setCurrentSpaceKey } = useWmms();

  // State cho bộ lọc trạng thái và tìm kiếm (Fields #2 & #3)
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [searchKeyword, setSearchKeyword] = useState('');

  // Lọc danh sách đơn hàng theo Status và Keyword (UC-34)
  const filteredOrders = useMemo(() => {
    return orderSpaces.filter((ord) => {
      if (statusFilter !== 'ALL' && ord.lifecycleStatusBadge !== statusFilter) {
        return false;
      }
      if (searchKeyword.trim()) {
        const kw = searchKeyword.trim().toLowerCase();
        const match =
          ord.spaceKey.toLowerCase().includes(kw) ||
          ord.customerName.toLowerCase().includes(kw) ||
          ord.poNumber.toLowerCase().includes(kw) ||
          ord.lineItemsVolumeSummary.toLowerCase().includes(kw);
        if (!match) return false;
      }
      return true;
    });
  }, [orderSpaces, statusFilter, searchKeyword]);

  const getLifecycleBadgeClass = (badge: string) => {
    if (badge === 'IN_PRODUCTION')
      return 'bg-blue-50 text-blue-700 border-blue-200';
    if (badge === 'BOM_SETTING')
      return 'bg-amber-50 text-amber-800 border-amber-200';
    if (badge === 'NEWLY_RECEIVED')
      return 'bg-slate-100 text-slate-700 border-slate-200';
    return 'bg-emerald-50 text-emerald-700 border-emerald-200';
  };

  const getLifecycleDotClass = (badge: string) => {
    if (badge === 'IN_PRODUCTION') return 'bg-blue-600';
    if (badge === 'BOM_SETTING') return 'bg-amber-500';
    if (badge === 'NEWLY_RECEIVED') return 'bg-slate-500';
    return 'bg-emerald-500';
  };

  const getEtdBadgeClass = (days: number) => {
    if (days <= 16)
      return 'bg-amber-50 text-amber-800 border-amber-200';
    return 'bg-emerald-50 text-emerald-700 border-emerald-200';
  };

  const handleOpenSpace = (spaceKey: string) => {
    setCurrentSpaceKey(spaceKey);
    navigate(`/orders/spaces/${spaceKey}`);
  };

  return (
    <WmmsAppShell mode="sales">
      <div className="max-w-[1400px] mx-auto space-y-6">
        {/* Page Heading + Primary CTA */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
              Danh mục đơn hàng bán
            </h1>
            <p className="text-sm text-slate-600 mt-1">
              Theo dõi tiến độ thực hiện PO của khách hàng, ngày giao dự kiến (ETD) và tiếp nhận PO Excel qua Staging.
            </p>
          </div>

          <button
            type="button"
            onClick={() => navigate('/orders/staging')}
            className="px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition self-start sm:self-auto flex items-center gap-2 cursor-pointer"
          >
            <svg
              className="w-4 h-4 shrink-0"
              fill="none"
              stroke="currentColor"
              strokeWidth={2.5}
              viewBox="0 0 24 24"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            <span>Nhập PO Excel (Staging)</span>
          </button>
        </div>

        {/* 3 KPI Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                ĐƠN ĐANG THỰC HIỆN
              </span>
              <span className="w-8 h-8 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center">
                <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">
              {orderSpaces.length}
            </div>
            <div className="text-xs text-slate-500 mt-1">
              Đang triển khai sản xuất, lập BOM và mới tiếp nhận
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                ETD CẦN ƯU TIÊN (&lt; 10 NGÀY)
              </span>
              <span className="w-8 h-8 rounded-lg bg-rose-50 text-rose-600 flex items-center justify-center">
                <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-rose-600 mt-2">2</div>
            <div className="text-xs text-slate-500 mt-1">
              Ưu tiên cao trong kế hoạch điều độ xưởng
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                TỔNG SẢN PHẨM TRÊN XƯỞNG
              </span>
              <span className="w-8 h-8 rounded-lg bg-emerald-50 text-emerald-600 flex items-center justify-center">
                <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
                </svg>
              </span>
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-2">1,450</div>
            <div className="text-xs text-slate-500 mt-1">
              Ghế ăn, bàn ăn, khung giường xuất khẩu
            </div>
          </div>
        </div>

        {/* Orders Directory Card */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white">
            <h2 className="text-base font-bold text-slate-900">
              Đơn hàng chính thức &amp; Không gian sản xuất
            </h2>

            <div className="flex flex-wrap items-center gap-3">
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                aria-label="Lọc trạng thái đơn hàng"
                className="px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-600"
              >
                <option value="ALL">Tất cả trạng thái</option>
                <option value="IN_PRODUCTION">Đang sản xuất</option>
                <option value="BOM_SETTING">Đang lập BOM</option>
                <option value="NEWLY_RECEIVED">Mới nhận</option>
              </select>

              <div className="relative w-full sm:w-64">
                <svg
                  className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth={2}
                  viewBox="0 0 24 24"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
                <input
                  type="text"
                  value={searchKeyword}
                  onChange={(e) => setSearchKeyword(e.target.value)}
                  placeholder="Tìm khách hàng, số PO..."
                  className="w-full pl-9 pr-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
                />
              </div>
            </div>
          </div>

          <div className="overflow-x-auto bg-white">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-6 min-w-[170px]">Mã Order Space</th>
                  <th className="py-3.5 px-4 min-w-[220px]">Khách hàng / Người mua</th>
                  <th className="py-3.5 px-4 min-w-[160px]">Mã PO</th>
                  <th className="py-3.5 px-4 min-w-[180px]">ETD mục tiêu</th>
                  <th className="py-3.5 px-4 min-w-[180px]">Dòng hàng / Số lượng</th>
                  <th className="py-3.5 px-4 min-w-[160px]">Trạng thái</th>
                  <th className="py-3.5 px-6 text-right min-w-[130px]">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm bg-white">
                {filteredOrders.map((ord) => (
                  <tr key={ord.spaceKey} className="hover:bg-slate-50/90 transition-colors">
                    <td className="py-4 px-6 whitespace-nowrap">
                      <button
                        type="button"
                        onClick={() => handleOpenSpace(ord.spaceKey)}
                        className="font-bold text-blue-600 hover:text-blue-700 hover:underline cursor-pointer"
                      >
                        {ord.spaceKey}
                      </button>
                    </td>
                    <td className="py-4 px-4 font-semibold text-slate-900 whitespace-nowrap">
                      {ord.customerName}
                    </td>
                    <td className="py-4 px-4 font-mono text-sm text-slate-700 whitespace-nowrap">
                      {ord.poNumber}
                    </td>
                    <td className="py-4 px-4 whitespace-nowrap">
                      <span
                        className={`inline-block px-2.5 py-0.5 text-xs font-bold uppercase rounded border whitespace-nowrap ${getEtdBadgeClass(
                          ord.daysRemaining
                        )}`}
                      >
                        {ord.etdDate} ({ord.daysRemaining} NGÀY)
                      </span>
                    </td>
                    <td className="py-4 px-4 text-slate-700 whitespace-nowrap">
                      {ord.lineItemsVolumeSummary}
                    </td>
                    <td className="py-4 px-4 whitespace-nowrap">
                      <span
                        className={`inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-bold uppercase rounded-full border whitespace-nowrap ${getLifecycleBadgeClass(
                          ord.lifecycleStatusBadge
                        )}`}
                      >
                        <span
                          className={`w-1.5 h-1.5 rounded-full shrink-0 ${getLifecycleDotClass(
                            ord.lifecycleStatusBadge
                          )}`}
                        />
                        {lifecycleStatusLabel(ord.lifecycleStatusBadge)}
                      </span>
                    </td>
                    <td className="py-4 px-6 text-right whitespace-nowrap">
                      <button
                        type="button"
                        onClick={() => handleOpenSpace(ord.spaceKey)}
                        className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 transition cursor-pointer"
                      >
                        Mở Space →
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </WmmsAppShell>
  );
}
