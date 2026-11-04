import { apiClient } from "../../../lib/axios";
import type { AuditLogQueryParams } from "../types";

export const auditLogsApi = {
  /**
   * UC-13: Truy vấn danh sách nhật ký kiểm toán hệ thống có phân trang & bộ lọc
   */
  getAuditLogs: async (params: AuditLogQueryParams = {}) => {
    const res = await apiClient.get("/audit-logs", { params });
    return res.data;
  },

  /**
   * UC-14: Lấy chi tiết bản ghi kiểm toán kèm before/after state
   */
  getAuditLogById: async (id: number | string) => {
    const res = await apiClient.get(`/audit-logs/${id}`);
    return res.data;
  },

  /**
   * UC-13 / SCR-ADM-02: Xuất danh sách nhật ký kiểm toán ra file CSV
   */
  exportCsv: async (params: AuditLogQueryParams = {}) => {
    const res = await apiClient.get("/audit-logs/export-csv", {
      params,
      responseType: "blob",
    });
    return res.data;
  },
};

