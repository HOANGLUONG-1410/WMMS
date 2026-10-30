/**
 * DTO Chi tiết bản ghi nhật ký kiểm toán hệ thống (UC-13, UC-14)
 */
export interface AuditLogDto {
  logId: number;
  userId: number | null;
  username: string | null;
  userFullName: string | null;
  primaryRole: string | null;
  actionType: string;
  entityName: string;
  entityId: string;
  beforeState: string | null;
  afterState: string | null;
  reason: string | null;
  ipAddress: string | null;
  createdAt: string;
}

/**
 * Tham số lọc và phân trang nhật ký kiểm toán (UC-13)
 */
export interface AuditLogQueryParams {
  userId?: number;
  actionType?: string;
  entityName?: string;
  searchKeyword?: string;
  fromDate?: string;
  toDate?: string;
  pageNumber?: number;
  pageSize?: number;
}

