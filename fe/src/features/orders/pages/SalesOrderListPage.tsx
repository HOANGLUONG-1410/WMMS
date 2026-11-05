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

  // Định dạng huy hiệu Lifecycle Status giống hệt ảnh SCR-ORD-01
  const getLifecycleBadgeClass = (badge) => {
    if (badge === 'IN_PRODUCTION') return 'bg-blue-100 text-blue-700';
    if (badge === 'BOM_SETTING') return 'bg-yellow-100 text-amber-800';
    if (badge === 'NEWLY_RECEIVED') return 'bg-slate-100 text-slate-700';
    return 'bg-emerald-100 text-emerald-700';
  };

  // Định dạng huy hiệu Target ETD (Delivery)
  const getEtdBadgeClass = (days) => {
    if (days <= 16) return 'bg-yellow-100 text-amber-800';
    return 'bg-emerald-100 text-emerald-700';
  };

  /**
   * Field #5: Điều hướng sang màn hình Order Space Detail Dashboard (SCR-ORD-03)
   */
  const handleOpenSpace = (spaceKey) => {
    setCurrentSpaceKey(spaceKey);
    navigate(`/orders/spaces/${spaceKey}`);
  };

  return (
    <WmmsAppShell mode="sales">
      <div className="max-w-[1400px] mx-auto space-y-5">
        {/* Hàng tiêu đề + Field #1: + Nhập PO Excel của khách (Khu Staging) */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
              Danh mục đơn hàng bán
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Theo dõi tiến độ thực hiện PO của khách, ngày giao dự kiến (ETD) và mở màn hình nhập PO Excel qua Staging.
            </p>
          </div>

          <button
            type="button"
            onClick={() => navigate('/orders/staging')}
            className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition self-start sm:self-auto"
          >
            + Nhập PO Excel của khách (Khu Staging)
          </button>
        </div>

        {/* 3 Thẻ KPI Thống kê trên đầu màn hình SCR-ORD-01 */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              ĐƠN ĐANG THỰC HIỆN
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">8</div>
            <div className="text-[11px] text-slate-400 mt-1">
              4 đang sản xuất, 2 đang lập BOM, 2 mới nhận
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              ETD GẤP (&lt; 10 NGÀY)
            </div>
            <div className="text-2xl font-extrabold text-rose-600 mt-1">2</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Ưu tiên cao trong kế hoạch xưởng
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              TỔNG SỐ SẢN PHẨM TRÊN XƯỞNG
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">1,450</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Ghế ăn, bàn ăn, khung giường
            </div>
          </div>
        </div>

        {/* Bảng Official Orders & Production Spaces (Fields #2, #3, #4, #5) */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-5 py-3.5 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <h2 className="text-xs font-bold text-slate-800">
              Đơn hàng chính thức &amp; Không gian sản xuất
            </h2>

            <div className="flex flex-wrap items-center gap-2.5">
              {/* Field #2: Order Status Filter */}
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="ALL">Tất cả trạng thái</option>
                <option value="IN_PRODUCTION">Đang sản xuất</option>
                <option value="BOM_SETTING">Đang lập BOM</option>
                <option value="NEWLY_RECEIVED">Mới nhận</option>
              </select>

              {/* Field #3: Search customer, PO #... */}
              <input
                type="text"
                value={searchKeyword}
                onChange={(e) => setSearchKeyword(e.target.value)}
                placeholder="Tìm khách hàng, số PO..."
                className="w-full sm:w-56 px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>

          {/* Field #4: Official Orders & Production Spaces Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-5">Mã Order Space</th>
                  <th className="py-3 px-4">Khách hàng / Người mua</th>
                  <th className="py-3 px-4">Mã PO</th>
                  <th className="py-3 px-4">ETD mục tiêu (Giao hàng)</th>
                  <th className="py-3 px-4">Dòng hàng / Số lượng</th>
                  <th className="py-3 px-4">Trạng thái vòng đời</th>
                  <th className="py-3 px-5 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-xs">
                {filteredOrders.map((ord) => (
                  <tr key={ord.spaceKey} className="hover:bg-slate-50/80 transition-colors">
                    <td className="py-3.5 px-5">
                      <button
                        type="button"
                        onClick={() => handleOpenSpace(ord.spaceKey)}
                        className="font-bold text-blue-600 hover:text-blue-700 hover:underline"
                      >
                        {ord.spaceKey}
                      </button>
                    </td>
                    <td className="py-3.5 px-4 font-bold text-slate-900">{ord.customerName}</td>
                    <td className="py-3.5 px-4 font-mono text-[11px] text-slate-600">
                      {ord.poNumber}
                    </td>
                    <td className="py-3.5 px-4">
                      <span
                        className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded ${getEtdBadgeClass(
                          ord.daysRemaining
                        )}`}
                      >
                        {ord.etdDate} ({ord.daysRemaining} NGÀY)
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-slate-700">{ord.lineItemsVolumeSummary}</td>
                    <td className="py-3.5 px-4">
                      <span
                        className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded ${getLifecycleBadgeClass(
                          ord.lifecycleStatusBadge
                        )}`}
                      >
                        {lifecycleStatusLabel(ord.lifecycleStatusBadge)}
                      </span>
                    </td>
                    <td className="py-3.5 px-5 text-right">
                      {/* Field #5: Mở Space → */}
                      <button
                        type="button"
                        onClick={() => handleOpenSpace(ord.spaceKey)}
                        className="px-3 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition"
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
