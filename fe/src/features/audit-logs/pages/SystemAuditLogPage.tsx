import { useState } from "react";
import WmmsAppShell from "../../../components/layout/WmmsAppShell";
import { useAuditLogs } from "../hooks/useAuditLogs";
import { auditLogsApi } from "../api/auditLogsApi";
import type { AuditLogDto } from "../types";

/**
 * Màn hình III.3.4: System Audit Log Viewer — Immutable System Audit Logs (SCR-ADM-02)
 * Tuân thủ 100% thiết kế giao diện và bảng Field Description trong Report 3 (Section III.3.4):
 * - Tiêu đề: `Immutable System Audit Logs (SCR-ADM-02)`
 * - Phụ đề: `Complete chronological trace of high-risk actions, override authorizations, and data state transitions.`
 * - Field #1: Nút `Export CSV` ở góc phải trên cùng (gọi API /audit-logs/export-csv).
 * - Thanh Filter Logs:
 *   + Field #2: Dropdown `All Action Categories`
 *   + Field #3: Date Input
 *   + Field #4: Search Bar `Search by entity ID or user...`
 * - Field #5: Bảng dữ liệu `Audit Log Data Table` với 6 cột:
 *   `Timestamp` | `Actor & Role` | `Action Type` | `Target Entity` | `State Delta (Before → After)` | `Mandatory Justification / Notes`
 */
export default function SystemAuditLogPage() {
  // State bộ lọc trên thanh Filter Logs (Fields #2, #3, #4)
  const [actionCategory, setActionCategory] = useState("ALL");
  const [selectedDate, setSelectedDate] = useState("");
  const [searchKeyword, setSearchKeyword] = useState("");
  const [isExporting, setIsExporting] = useState(false);

  // State phân trang với TanStack Query
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);

  // Handlers thay đổi bộ lọc -> tự động reset về trang 1
  const handleCategoryChange = (val: string) => {
    setActionCategory(val);
    setPageNumber(1);
  };

  const handleDateChange = (val: string) => {
    setSelectedDate(val);
    setPageNumber(1);
  };

  const handleSearchChange = (val: string) => {
    setSearchKeyword(val);
    setPageNumber(1);
  };

  const handleClearFilters = () => {
    setActionCategory("ALL");
    setSelectedDate("");
    setSearchKeyword("");
    setPageNumber(1);
  };

  const handlePageSizeChange = (val: number) => {
    setPageSize(val);
    setPageNumber(1);
  };

  // Chuẩn bị params cho backend
  const filterParams = {
    actionType: actionCategory !== "ALL" ? actionCategory : undefined,
    fromDate: selectedDate ? `${selectedDate}T00:00:00` : undefined,
    toDate: selectedDate ? `${selectedDate}T23:59:59` : undefined,
    searchKeyword: searchKeyword.trim() || undefined,
    pageNumber,
    pageSize,
  };

  const { data: pagedLogs, isLoading, isFetching, isError, error } = useAuditLogs(filterParams);
  const logsList: AuditLogDto[] = pagedLogs?.items ?? [];
  const totalCount = pagedLogs?.totalCount ?? 0;
  const totalPages = pagedLogs?.totalPages ?? Math.max(1, Math.ceil(totalCount / pageSize));
  const hasPreviousPage = pagedLogs?.hasPreviousPage ?? pageNumber > 1;
  const hasNextPage = pagedLogs?.hasNextPage ?? pageNumber < totalPages;

  const startRecord = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endRecord = Math.min(pageNumber * pageSize, totalCount);

  // Tạo mảng số trang hiển thị thân thiện (e.g. 1, 2, ..., 10)
  const getVisiblePages = () => {
    const delta = 2;
    const range: (number | string)[] = [];
    const left = Math.max(2, pageNumber - delta);
    const right = Math.min(totalPages - 1, pageNumber + delta);

    range.push(1);
    if (left > 2) range.push("...");
    for (let i = left; i <= right; i++) {
      range.push(i);
    }
    if (right < totalPages - 1) range.push("...");
    if (totalPages > 1) range.push(totalPages);

    return range;
  };

  /**
   * Field #1: Xuất dữ liệu nhật ký kiểm toán ra file CSV qua API backend (UC-13 / SCR-ADM-02)
   */
  const handleExportCsv = async () => {
    try {
      setIsExporting(true);
      // Xuất toàn bộ bản ghi khớp bộ lọc (tối đa 10,000) thay vì chỉ trang hiện tại
      const exportParams = {
        actionType: actionCategory !== "ALL" ? actionCategory : undefined,
        fromDate: selectedDate ? `${selectedDate}T00:00:00` : undefined,
        toDate: selectedDate ? `${selectedDate}T23:59:59` : undefined,
        searchKeyword: searchKeyword.trim() || undefined,
        pageNumber: 1,
        pageSize: 10000,
      };
      const blobData = await auditLogsApi.exportCsv(exportParams);
      const blob = new Blob([blobData], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `WMMS_AuditLogs_${selectedDate || "export"}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      // Fallback export từ danh sách hiện có nếu endpoint blob gặp lỗi mạng
      const headers = [
        "Timestamp",
        "Actor",
        "Role",
        "ActionType",
        "TargetEntity",
        "StateDelta",
        "MandatoryJustification",
      ];
      const rows = logsList.map((l) => [
        l.createdAt,
        l.userFullName || l.username || "System",
        l.primaryRole || "SYSTEM",
        l.actionType,
        `${l.entityName} #${l.entityId}`,
        `${l.beforeState || "NONE"} → ${l.afterState || "NONE"}`,
        l.reason || "",
      ]);

      const csvContent =
        "\uFEFF" +
        [headers, ...rows]
          .map((r) =>
            r.map((cell) => `"${String(cell || "").replace(/"/g, '""')}"`).join(","),
          )
          .join("\n");

      const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `WMMS_AuditLogs_${selectedDate || "export"}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } finally {
      setIsExporting(false);
    }
  };

  // Màu sắc huy hiệu Action Type giống hệt ảnh SCR-ADM-02
  const getActionBadgeStyle = (actionType: string) => {
    const upper = String(actionType || "").toUpperCase();
    if (upper.includes("OVERDRIVER") || upper.includes("BAN") || upper.includes("LOCKED"))
      return "bg-rose-100 text-rose-700";
    if (upper.includes("STAGING") || upper.includes("CONFIRM") || upper.includes("CREATE"))
      return "bg-blue-100 text-blue-700";
    if (upper.includes("SCRAP") || upper.includes("PR") || upper.includes("UPDATE"))
      return "bg-yellow-100 text-amber-800";
    if (upper.includes("RECEIPT") || upper.includes("COMMIT"))
      return "bg-emerald-100 text-emerald-700";
    return "bg-slate-100 text-slate-700";
  };

  // Màu sắc huy hiệu Actor Role
  const getActorRoleBadgeStyle = (role: string) => {
    const upper = String(role || "").toUpperCase();
    if (upper === "OWNER") return "bg-yellow-100 text-amber-800";
    if (upper === "ADMIN") return "bg-slate-700 text-white";
    if (upper === "SALES") return "bg-blue-100 text-blue-700";
    if (upper === "WAREHOUSE") return "bg-orange-100 text-orange-800";
    return "bg-slate-100 text-slate-700";
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
            disabled={isExporting}
            className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md shadow-sm transition self-start sm:self-auto disabled:opacity-60 flex items-center gap-1.5"
          >
            {isExporting && (
              <span className="w-3.5 h-3.5 border-2 border-slate-700 border-t-transparent rounded-full animate-spin" />
            )}
            {isExporting ? "Exporting..." : "Export CSV"}
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
              onChange={(e) => handleCategoryChange(e.target.value)}
              className="px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="ALL">All Action Categories</option>
              <option value="USER_CREATED">USER_CREATED</option>
              <option value="USER_UPDATED">USER_UPDATED</option>
              <option value="USER_STATUS_CHANGED">USER_STATUS_CHANGED</option>
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
              onChange={(e) => handleDateChange(e.target.value)}
              className="px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />

            {/* Field #4: Search by entity ID or user... */}
            <input
              type="text"
              value={searchKeyword}
              onChange={(e) => handleSearchChange(e.target.value)}
              placeholder="Search by entity ID or user..."
              className="w-full sm:w-64 px-3 py-1.5 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />

            {(actionCategory !== "ALL" || selectedDate || searchKeyword) && (
              <button
                type="button"
                onClick={handleClearFilters}
                className="text-xs text-blue-600 hover:text-blue-800 underline font-medium ml-auto"
              >
                Clear Filters
              </button>
            )}
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
                {isLoading && (
                  <tr>
                    <td colSpan={6} className="py-12 text-center text-slate-400">
                      <div className="inline-block w-6 h-6 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mb-2" />
                      <p className="text-xs">Fetching immutable audit logs...</p>
                    </td>
                  </tr>
                )}

                {isError && (
                  <tr>
                    <td colSpan={6} className="py-8 text-center text-rose-600 font-medium">
                      {(error as { message?: string })?.message ||
                        "Unable to load audit logs. Please verify network or session."}
                    </td>
                  </tr>
                )}

                {!isLoading && !isError && logsList.length === 0 && (
                  <tr>
                    <td colSpan={6} className="py-8 text-center text-slate-400 italic">
                      No system audit log records found matching your filters.
                    </td>
                  </tr>
                )}

                {!isLoading &&
                  !isError &&
                  logsList.map((log) => {
                    const formattedDate = new Date(log.createdAt).toLocaleString("en-GB", {
                      year: "numeric",
                      month: "2-digit",
                      day: "2-digit",
                      hour: "2-digit",
                      minute: "2-digit",
                      second: "2-digit",
                    });
                    const [datePart, timePart] = formattedDate.split(", ");
                    const actorName = log.userFullName || log.username || "System Actor";
                    const actorRole = log.primaryRole || "SYSTEM";
                    const targetEntity = `${log.entityName} ${log.entityId ? `#${log.entityId}` : ""}`.trim();
                    const stateDelta =
                      log.beforeState || log.afterState
                        ? `${log.beforeState || "NONE"} → ${log.afterState || "NONE"}`
                        : "STATE_IMMUTABLE";

                    return (
                      <tr key={log.logId} className="hover:bg-slate-50/80 transition-colors">
                        <td className="py-3.5 px-5 font-mono text-[11px] text-slate-700 leading-snug">
                          <div>{datePart}</div>
                          <div className="text-slate-400">{timePart}</div>
                        </td>
                        <td className="py-3.5 px-4">
                          <div className="font-bold text-slate-900 leading-snug">{actorName}</div>
                          <span
                            className={`inline-block mt-1 px-1.5 py-0.5 text-[9px] font-bold uppercase rounded ${getActorRoleBadgeStyle(
                              actorRole,
                            )}`}
                          >
                            {actorRole}
                          </span>
                        </td>
                        <td className="py-3.5 px-4">
                          <span
                            className={`inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded ${getActionBadgeStyle(
                              log.actionType,
                            )}`}
                          >
                            {log.actionType}
                          </span>
                        </td>
                        <td className="py-3.5 px-4 font-medium text-slate-800">{targetEntity}</td>
                        <td className="py-3.5 px-4 font-mono text-[11px] text-slate-700">
                          {stateDelta}
                        </td>
                        <td className="py-3.5 px-5 italic text-slate-600">
                          {log.reason || "N/A"}
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>

          {/* Field #6: Thanh phân trang Paging với TanStack Query */}
          <div className="px-5 py-3 border-t border-slate-200 bg-slate-50/70 flex flex-col md:flex-row items-center justify-between gap-3 text-xs text-slate-600">
            {/* Vùng bên trái: Thống kê bản ghi & Dropdown chọn 10/30/50 record / 1 trang */}
            <div className="flex flex-wrap items-center gap-4">
              <div>
                Showing <span className="font-semibold text-slate-900">{startRecord}</span> to{" "}
                <span className="font-semibold text-slate-900">{endRecord}</span> of{" "}
                <span className="font-semibold text-slate-900">{totalCount}</span> records
              </div>

              {/* Dropdown chọn hiển thị 10/30/50 record / 1 trang */}
              <div className="flex items-center gap-1.5">
                <label htmlFor="pageSizeSelect" className="text-slate-500 font-medium">
                  Show:
                </label>
                <select
                  id="pageSizeSelect"
                  value={pageSize}
                  onChange={(e) => handlePageSizeChange(Number(e.target.value))}
                  className="px-2.5 py-1 text-xs bg-white border border-slate-300 rounded shadow-xs focus:outline-none focus:ring-2 focus:ring-blue-500 font-medium text-slate-700 cursor-pointer"
                >
                  <option value={10}>10 records / page</option>
                  <option value={30}>30 records / page</option>
                  <option value={50}>50 records / page</option>
                </select>
              </div>

              {isFetching && !isLoading && (
                <span className="inline-flex items-center gap-1.5 text-blue-600 text-[11px] font-medium">
                  <span className="w-2.5 h-2.5 border-2 border-blue-600 border-t-transparent rounded-full animate-spin" />
                  Updating...
                </span>
              )}
            </div>

            {/* Vùng bên phải: Nút chuyển trang (First, Prev, Page numbers, Next, Last) */}
            <div className="flex items-center gap-1">
              <button
                type="button"
                onClick={() => setPageNumber(1)}
                disabled={pageNumber <= 1 || isLoading}
                className="px-2 py-1 text-xs font-semibold rounded border border-slate-200 bg-white hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed transition text-slate-700"
                title="First Page"
              >
                «
              </button>
              <button
                type="button"
                onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                disabled={!hasPreviousPage || pageNumber <= 1 || isLoading}
                className="px-2.5 py-1 text-xs font-semibold rounded border border-slate-200 bg-white hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed transition text-slate-700"
              >
                Previous
              </button>

              <div className="flex items-center gap-1 px-1">
                {getVisiblePages().map((item, idx) =>
                  item === "..." ? (
                    <span key={`dots-${idx}`} className="px-1.5 py-1 text-slate-400 font-medium">
                      ...
                    </span>
                  ) : (
                    <button
                      key={`page-${item}`}
                      type="button"
                      onClick={() => setPageNumber(Number(item))}
                      disabled={isLoading}
                      className={`min-w-7 px-2 py-1 text-xs font-bold rounded transition ${
                        item === pageNumber
                          ? "bg-blue-600 text-white shadow-xs"
                          : "border border-slate-200 bg-white hover:bg-slate-100 text-slate-700"
                      }`}
                    >
                      {item}
                    </button>
                  ),
                )}
              </div>

              <button
                type="button"
                onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
                disabled={!hasNextPage || pageNumber >= totalPages || isLoading}
                className="px-2.5 py-1 text-xs font-semibold rounded border border-slate-200 bg-white hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed transition text-slate-700"
              >
                Next
              </button>
              <button
                type="button"
                onClick={() => setPageNumber(totalPages)}
                disabled={pageNumber >= totalPages || isLoading}
                className="px-2.5 py-1 text-xs font-semibold rounded border border-slate-200 bg-white hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed transition text-slate-700"
                title="Last Page"
              >
                »
              </button>
            </div>
          </div>
        </div>
      </div>
    </WmmsAppShell>
  );
}
