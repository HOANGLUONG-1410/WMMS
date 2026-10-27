import { apiClient } from "../../../lib/axios";

export const adminApi = {
  /** UC-06: Xem danh sách tài khoản nhân sự kèm bộ lọc tìm kiếm */
  getUsers: async (params: Record<string, unknown> = {}) => {
    const res = await apiClient.get("/users", { params });
    return res.data;
  },

  /** UC-07: Xem chi tiết tài khoản nhân sự */
  getUserById: async (userId: number | string) => {
    const res = await apiClient.get(`/users/${userId}`);
    return res.data;
  },

  /** UC-08: Tạo tài khoản nhân sự mới (MOD-ADM-01) */
  createUser: async (payload: Record<string, unknown>) => {
    const res = await apiClient.post("/users", payload);
    return res.data;
  },

  /** UC-09: Cập nhật thông tin nhân sự và vai trò */
  updateUser: async (
    userId: number | string,
    payload: Record<string, unknown>,
  ) => {
    const res = await apiClient.put(`/users/${userId}`, payload);
    return res.data;
  },

  /** UC-09: Cập nhật danh sách đa vai trò */
  updateUserRoles: async (userId: number | string, roleCodes: string[]) => {
    const res = await apiClient.put(`/users/${userId}/roles`, { roleCodes });
    return res.data;
  },

  /** UC-10: Khóa (BANNED) hoặc Mở khóa (ACTIVE) tài khoản nhân sự */
  updateUserStatus: async (
    userId: number | string,
    status: string,
    reason: string,
  ) => {
    const res = await apiClient.patch(`/users/${userId}/status`, {
      status,
      reason,
    });
    return res.data;
  },

  /** UC-11: Lấy danh sách vai trò và cấu hình quyền RBAC */
  getRoles: async () => {
    const res = await apiClient.get("/roles");
    return res.data;
  },

  /** UC-12: Cập nhật ma trận quyền cho vai trò */
  updateRolePermissions: async (
    roleId: number | string,
    permissionsJson: string,
    reason?: string,
  ) => {
    const res = await apiClient.put(`/roles/${roleId}/permissions`, {
      permissionsJson,
      reason,
    });
    return res.data;
  },
};
