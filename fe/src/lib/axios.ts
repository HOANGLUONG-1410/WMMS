import axios from "axios";
import { env } from "../constants/env";
import { ApiError, type ApiResponseEnvelope } from "./api/apiError";
import { HttpStatus } from "./api/httpStatus";
import { getAccessToken } from "../features/auth/store/tokenStore";

export const apiClient = axios.create({
  baseURL: env.API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: true,
});

// Tự động đính kèm JWT Access Token vào header Authorization (BR-AUT-04)
apiClient.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Chuẩn hóa mọi lỗi phản hồi từ Backend về ApiError để TanStack Query (QueryCache / MutationCache) xử lý nhất quán
apiClient.interceptors.response.use(
  (response) => {
    const body = response.data as ApiResponseEnvelope | undefined;
    if (body && typeof body === "object" && body.success === false) {
      const normalizedErrors = Array.isArray(body.errors)
        ? body.errors.map(String)
        : body.errors && typeof body.errors === "object"
          ? Object.values(body.errors).flat().map(String)
          : [];

      return Promise.reject(
        new ApiError(
          body.message || normalizedErrors[0] || "Request failed.",
          response.status || HttpStatus.BAD_REQUEST,
          body.messageCode,
          normalizedErrors,
          response.config?.url,
        ),
      );
    }
    return response;
  },
  (error: unknown) => {
    return Promise.reject(ApiError.fromAxiosError(error));
  },
);
