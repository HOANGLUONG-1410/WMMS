/**
 * Cấu trúc User trả về từ API /users (UC-06)
 */
export interface UserDto {
  userId: number;
  username: string;
  email: string;
  fullName: string;
  phone: string;
  status: "ACTIVE" | "SUSPENDED" | "BANNED" | string;
  failedLoginCount: number;
  lockedUntil: string | null;
  lastLoginAt: string | null;
  createdAt: string;
  updatedAt: string;
  roles: string[];
}

/**
 * Cấu trúc phân trang chuẩn của hệ thống WMMS
 */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

/**
 * Tham số truy vấn khi lọc / tìm kiếm danh sách nhân sự
 */
export interface UserQueryParams {
  searchTerm?: string;
  roleCode?: string;
  status?: string;
  pageIndex?: number;
  pageSize?: number;
}

