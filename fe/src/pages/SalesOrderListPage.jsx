import React, { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import WmmsAppShell from '../common/WmmsAppShell';
import { useWmms } from '../context/WmmsContext';

/**
 * Màn hình III.4.1: Sales Order List Screen — Sales Orders Directory (SCR-ORD-01)
 * Tuân thủ 100% thiết kế giao diện và bảng Field Description trong Report 3 (Section III.4.1):
 * - Tiêu đề: `Sales Orders Directory (SCR-ORD-01)`
 * - Phụ đề: `Track customer PO fulfillment, target ETD ship dates, and launch Excel staging intake.`
 * - Field #1: Nút `+ Import Customer Excel PO (Staging Area)` màu xanh ở góc phải trên cùng.
 * - 3 Thẻ KPI:
 *   + `ACTIVE ORDERS`: `8` (`4 In Production, 2 BOM Setting, 2 Newly Received`)
 *   + `URGENT ETD (< 10 DAYS)`: `2` (`High priority for factory schedule`)
 *   + `TOTAL UNITS ON FLOOR`: `1,450` (`Dining chairs, tables, bed frames`)
 * - Bảng `Official Orders & Production Spaces`:
 *   + Field #2: Dropdown `Order Status Filter` (`All Statuses`)
 *   + Field #3: Search Bar `Search customer, PO #...`
 *   + Field #4: Bảng dữ liệu với 6 cột: `Order Space Key`, `Customer / Buyer`, `PO Reference`, `Target ETD (Delivery)`, `Line Items / Volume`, `Lifecycle Status`, `Action`
 *   + Field #5: Nút `Open Space →` mở màn hình `Order Space Detail Dashboard (SCR-ORD-03)`.
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
        {/* Hàng tiêu đề + Field #1: + Import Customer Excel PO (Staging Area) */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
              Sales Orders Directory (SCR-ORD-01)
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Track customer PO fulfillment, target ETD ship dates, and launch Excel staging intake.
            </p>
          </div>

          <button
            type="button"
            onClick={() => navigate('/orders/staging')}
            className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition self-start sm:self-auto"
          >
            + Import Customer Excel PO (Staging Area)
          </button>
        </div>

        {/* 3 Thẻ KPI Thống kê trên đầu màn hình SCR-ORD-01 */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              ACTIVE ORDERS
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">8</div>
            <div className="text-[11px] text-slate-400 mt-1">
              4 In Production, 2 BOM Setting, 2 Newly Received
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              URGENT ETD (&lt; 10 DAYS)
            </div>
            <div className="text-2xl font-extrabold text-rose-600 mt-1">2</div>
            <div className="text-[11px] text-slate-400 mt-1">
              High priority for factory schedule
            </div>
          </div>

          <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              TOTAL UNITS ON FLOOR
            </div>
            <div className="text-2xl font-extrabold text-slate-900 mt-1">1,450</div>
            <div className="text-[11px] text-slate-400 mt-1">
              Dining chairs, tables, bed frames
            </div>
          </div>
        </div>

        {/* Bảng Official Orders & Production Spaces (Fields #2, #3, #4, #5) */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-5 py-3.5 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <h2 className="text-xs font-bold text-slate-800">
              Official Orders &amp; Production Spaces
            </h2>

            <div className="flex flex-wrap items-center gap-2.5">
              {/* Field #2: Order Status Filter */}
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="ALL">All Statuses</option>
                <option value="IN_PRODUCTION">IN_PRODUCTION</option>
                <option value="BOM_SETTING">BOM_SETTING</option>
                <option value="NEWLY_RECEIVED">NEWLY_RECEIVED</option>
              </select>

              {/* Field #3: Search customer, PO #... */}
              <input
                type="text"
                value={searchKeyword}
                onChange={(e) => setSearchKeyword(e.target.value)}
                placeholder="Search customer, PO #..."
                className="w-full sm:w-56 px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>

          {/* Field #4: Official Orders & Production Spaces Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-5">Order Space Key</th>
                  <th className="py-3 px-4">Customer / Buyer</th>
                  <th className="py-3 px-4">PO Reference</th>
                  <th className="py-3 px-4">Target ETD (Delivery)</th>
                  <th className="py-3 px-4">Line Items / Volume</th>
                  <th className="py-3 px-4">Lifecycle Status</th>
                  <th className="py-3 px-5 text-right">Action</th>
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
                        {ord.etdDate} ({ord.daysRemaining} DAYS)
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-slate-700">{ord.lineItemsVolumeSummary}</td>
                    <td className="py-3.5 px-4">
                      <span
                        className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded ${getLifecycleBadgeClass(
                          ord.lifecycleStatusBadge
                        )}`}
                      >
                        {ord.lifecycleStatusBadge}
                      </span>
                    </td>
                    <td className="py-3.5 px-5 text-right">
                      {/* Field #5: Open Space → */}
                      <button
                        type="button"
                        onClick={() => handleOpenSpace(ord.spaceKey)}
                        className="px-3 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition"
                      >
                        Open Space →
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
