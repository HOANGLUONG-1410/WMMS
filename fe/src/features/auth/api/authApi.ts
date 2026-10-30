import { apiClient } from "../../../lib/axios";
import type { ChangePasswordRequest, ResetPasswordRequest } from "../types";

export const authApi = {
  login: async (
    usernameOrEmail: string,
    password: string,
    rememberMe = false,
  ) => {
    const res = await apiClient.post("/auth/login", {
      usernameOrEmail,
      password,
      rememberMe,
    });
    return res.data;
  },

  logout: async () => {
    const res = await apiClient.post("/auth/logout", {});
    return res.data;
  },

  refreshToken: async (refreshToken = "") => {
    const res = await apiClient.post("/auth/refresh", { refreshToken });
    return res.data;
  },

  changePassword: async (payload: ChangePasswordRequest) => {
    const res = await apiClient.post("/auth/change-password", payload);
    return res.data;
  },

  forgotPassword: async (email: string) => {
    const res = await apiClient.post("/auth/forgot-password", { email });
    return res.data;
  },

  resetPassword: async (payload: ResetPasswordRequest) => {
    const res = await apiClient.post("/auth/reset-password", payload);
    return res.data;
  },

  getMe: async () => {
    const res = await apiClient.get("/auth/me");
    return res.data;
  },
};
