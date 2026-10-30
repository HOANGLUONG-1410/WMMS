import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../api/userApi";
import type { PagedResult, UserDto, UserQueryParams } from "../types";

export function useUsers(params: UserQueryParams = {}) {
  return useQuery<PagedResult<UserDto>>({
    queryKey: ["users", params],
    queryFn: async () => {
      const res = await adminApi.getUsers(
        params as unknown as Record<string, unknown>,
      );
      // Backend ApiResponse<PagedResult<UserDto>> bọc data trong res.data
      return res.data;
    },
    staleTime: 1000 * 30, // 30s cache
  });
}

