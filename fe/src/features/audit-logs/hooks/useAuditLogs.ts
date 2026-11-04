import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { auditLogsApi } from "../api/auditLogsApi";
import type { AuditLogDto, AuditLogQueryParams } from "../types";
import type { PagedResult } from "../../users/types";

export function useAuditLogs(params: AuditLogQueryParams = {}) {
  return useQuery<PagedResult<AuditLogDto>>({
    queryKey: ["audit-logs", params],
    queryFn: async () => {
      const res = await auditLogsApi.getAuditLogs(params);
      return res.data;
    },
    placeholderData: keepPreviousData,
    staleTime: 1000 * 30, // 30s
  });
}

