import { useMemo, useState } from 'react';
import WmmsAppShell from '../../../components/layout/WmmsAppShell';
import { useWmms } from '../../auth/hooks/WmmsContext';

/**
 * Màn hình III.3.4: System Audit Log Viewer — Immutable System Audit Logs (SCR-ADM-02)
 * Tuân thủ 100% thiết kế giao diện và bảng Field Description trong Report 3 (Section III.3.4):
 * - Tiêu đề: `Immutable System Audit Logs (SCR-ADM-02)`
 * - Phụ đề: `Complete chronological trace of high-risk actions, override authorizations, and data state transitions.`
 * - Field #1: Nút `Export CSV` ở góc phải trên cùng.
 * - Thanh Filter Logs:
 *   + Field #2: Dropdown `All Action Categories`
 *   + Field #3: Date Input (`10/20/2026`)
 *   + Field #4: Search Bar `Search by entity ID or user...`
 * - Field #5: Bảng dữ liệu `Audit Log Data Table` với 6 cột:
 *   `Timestamp` | `Actor & Role` | `Action Type` | `Target Entity` | `State Delta (Before → After)` | `Mandatory Justification / Notes`
 */
export default function SystemAuditLogPage() {
  const { auditLogs } = useWmms();

  // State bộ lọc trên thanh Filter Logs (Fields #2, #3, #4)
  const [actionCategory, setActionCategory] = useState('ALL');
  const [selectedDate, setSelectedDate] = useState('2026-10-20');
  const [searchKeyword, setSearchKeyword] = useState('');

  // Lọc danh sách nhật ký kiểm toán theo Category và Keyword
  const filteredLogs = useMemo(() => {
    return auditLogs.filter((log) => {
      if (actionCategory !== 'ALL' && !log.actionType.includes(actionCategory)) {
        return false;
      }
      if (searchKeyword.trim()) {
        const kw = searchKeyword.trim().toLowerCase();
        const match =
          log.actorName.toLowerCase().includes(kw) ||
          log.actorRole.toLowerCase().includes(kw) ||
          log.actionType.toLowerCase().includes(kw) ||
          log.targetEntity.toLowerCase().includes(kw) ||
          log.reason.toLowerCase().includes(kw);
        if (!match) return false;
      }
      return true;
    });
  }, [auditLogs, actionCategory, searchKeyword]);

  /**
   * Field #1: Xuất dữ liệu nhật ký kiểm toán hiện tại ra file CSV (UC-13 / SCR-ADM-02)
   */
  const handleExportCsv = () => {
    const headers = [
      'Timestamp',
      'Actor',
      'Role',
      'ActionType',
      'TargetEntity',
      'StateDelta',
      'MandatoryJustification'
    ];
    const rows = filteredLogs.map((l) => [
      l.timestamp,
      l.actorName,
      l.actorRole,
      l.actionType,
      l.targetEntity,
      l.stateDelta,
      l.reason
    ]);

    const csvContent =
      '\uFEFF' +
      [headers, ...rows]
        .map((r) => r.map((cell) => `"${String(cell || '').replace(/"/g, '""')}"`).join(','))
        .join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `WMMS_AuditLogs_${selectedDate || 'export'}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  // Màu sắc huy hiệu Action Type giống hệt ảnh SCR-ADM-02
  const getActionBadgeStyle = (actionType) => {
    if (actionType.includes('OVERDRIVER')) return 'bg-rose-100 text-rose-700';
    if (actionType.includes('STAGING') || actionType.includes('CONFIRM'))
      return 'bg-blue-100 text-blue-700';
    if (actionType.includes('SCRAP') || actionType.includes('PR'))
      return 'bg-yellow-100 text-amber-800';
    if (actionType.includes('RECEIPT') || actionType.includes('COMMIT'))
      return 'bg-emerald-100 text-emerald-700';
    return 'bg-slate-100 text-slate-700';
  };

  // Màu sắc huy hiệu Actor Role
  const getActorRoleBadgeStyle = (role) => {
    if (role === 'OWNER') return 'bg-yellow-100 text-amber-800';
    if (role === 'SALES') return 'bg-blue-100 text-blue-700';
    if (role === 'WAREHOUSE') return 'bg-orange-100 text-orange-800';
    return 'bg-slate-100 text-slate-700';
  };

  return (
    <WmmsAppShell mode="admin">
      <div className="max-w-[1400px] mx-auto space-y-5">
        {/* Header + Field #1: Export CSV Button */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
              Immutable System Audit Logs (SCR-ADM-02)
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Complete chronological trace of high-risk actions, override authorizations, and data
              state transitions.
            </p>
          </div>

          <button
            type="button"
            onClick={handleExportCsv}
            className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md shadow-sm transition self-start sm:self-auto"
          >
            Export CSV
          </button>
        </div>

        {/* Khung bảng Audit Log + Thanh Filter Logs (Fields #2, #3, #4, #5) */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
          {/* Thanh Filter Logs */}
          <div className="px-5 py-3.5 border-b border-slate-200 flex flex-wrap items-center gap-3">
            <span className="text-xs font-bold text-slate-700">Filter Logs:</span>

            {/* Field #2: Action Categories Filter */}
            <select
              value={actionCategory}
              onChange={(e) => setActionCategory(e.target.value)}
              className="px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="ALL">All Action Categories</option>
              <option value="AUTHORIZE_OVERDRIVER">AUTHORIZE_OVERDRIVER</option>
              <option value="CONFIRM_PO_STAGING">CONFIRM_PO_STAGING</option>
              <option value="APPROVE_SCRAP_REPLACEMENT">APPROVE_SCRAP_REPLACEMENT</option>
              <option value="COMMIT_GOODS_RECEIPT">COMMIT_GOODS_RECEIPT</option>
              <option value="APPROVE_CONSOLIDATED_PR">APPROVE_CONSOLIDATED_PR</option>
            </select>

            {/* Field #3: Date Filter */}
            <input
              type="date"
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
              className="px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />

            {/* Field #4: Search by entity ID or user... */}
            <input
              type="text"
              value={searchKeyword}
              onChange={(e) => setSearchKeyword(e.target.value)}
              placeholder="Search by entity ID or user..."
              className="w-full sm:w-64 px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #5: Audit Log Data Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-5 w-36">Timestamp</th>
                  <th className="py-3 px-4 w-40">Actor &amp; Role</th>
                  <th className="py-3 px-4 w-56">Action Type</th>
                  <th className="py-3 px-4 w-48">Target Entity</th>
                  <th className="py-3 px-4 w-64">State Delta (Before → After)</th>
                  <th className="py-3 px-5">Mandatory Justification / Notes</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-xs">
                {filteredLogs.map((log) => {
                  const [datePart, timePart] = String(log.timestamp || '').split(' ');
                  return (
                    <tr key={log.logId} className="hover:bg-slate-50/80 transition-colors">
                      <td className="py-3.5 px-5 font-mono text-[11px] text-slate-700 leading-snug">
                        <div>{datePart}</div>
                        <div>{timePart}</div>
                      </td>
                      <td className="py-3.5 px-4">
                        <div className="font-bold text-slate-900 leading-snug">{log.actorName}</div>
                        <span
                          className={`inline-block mt-1 px-1.5 py-0.5 text-[9px] font-bold uppercase rounded ${getActorRoleBadgeStyle(
                            log.actorRole
                          )}`}
                        >
                          {log.actorRole}
                        </span>
                      </td>
                      <td className="py-3.5 px-4">
                        <span
                          className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded ${getActionBadgeStyle(
                            log.actionType
                          )}`}
                        >
                          {log.actionType}
                        </span>
                      </td>
                      <td className="py-3.5 px-4 font-medium text-slate-800">{log.targetEntity}</td>
                      <td className="py-3.5 px-4 font-mono text-[11px] text-slate-700">
                        {log.stateDelta}
                      </td>
                      <td className="py-3.5 px-5 italic text-slate-600">{log.reason}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </WmmsAppShell>
  );
}
