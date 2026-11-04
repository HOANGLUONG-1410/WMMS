import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../api/userApi";
import type { PagedResult, UserDto, UserQueryParams } from "../types";

export function useUsers(params: UserQueryParams = {}) {
  const normalizedParams = {
    searchKeyword: params.searchKeyword || params.searchTerm || undefined,
    roleCode: params.roleCode || undefined,
    status: params.status || undefined,
    pageNumber: params.pageNumber ?? 1,
    pageSize: params.pageSize ?? 10,
  };

  return useQuery<PagedResult<UserDto>>({
    queryKey: ["users", normalizedParams],
    queryFn: async () => {
      const res = await adminApi.getUsers(
        normalizedParams as unknown as Record<string, unknown>,
      );
      return res.data;
    },
    // Cache các trang đã tải trong 5 phút để khi quay lại trang cũ không cần request lại Backend
    staleTime: 1000 * 60 * 5,
    gcTime: 1000 * 60 * 10,
  });
}
