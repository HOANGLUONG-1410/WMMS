// src/lib/queryClient.ts
import { QueryCache, MutationCache, QueryClient } from "@tanstack/react-query";
import { ApiErrorHandler } from "./api/apiErrorHandler";
import { ApiError } from "./api/apiError";

export const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: ApiErrorHandler.handle }),
  mutationCache: new MutationCache({
    onError: (error, _variables, _context, mutation) => {
      // Cho phép mutation bỏ qua toast lỗi toàn cục nếu đã xử lý inline trên UI (ví dụ màn đăng nhập)
      if (mutation?.meta?.skipGlobalErrorToast) return;
      ApiErrorHandler.handle(error);
    },
  }),
  defaultOptions: {
    queries: {
      retry: (count, err) =>
        !(err instanceof ApiError && err.status >= 400 && err.status < 500) &&
        count < 2,
      refetchOnWindowFocus: false,
    },
  },
});
