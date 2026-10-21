import axios from 'axios';
import { API_BASE_URL } from '../constant/wmmsConstants';

/**
 * Dịch vụ kết nối REST API tới WMMS Backend (.NET 8 Web API)
 * Bao gồm các nhóm API:
 * 1. Authorization (UC-01..UC-05): /api/v1/auth/*
 * 2. System Administration & RBAC (UC-06..UC-15): /api/v1/users/*, /api/v1/roles/*, /api/v1/audit-logs/*
 * 3. Order Intake & Order Space (UC-30..UC-35): /api/v1/orders/*
 */
const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  },
  withCredentials: true
});

// Tự động đính kèm JWT Access Token vào header Authorization (BR-AUT-04)
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('wmms_access_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/**
 * 1. API Phân hệ Xác thực (Section III.1: Authorization - UC-01..UC-05)
 */
export const authApi = {
  /** UC-01: Đăng nhập hệ thống bằng Email/Username và Password */
  login: async (usernameOrEmail, password, rememberMe = false) => {
    const res = await apiClient.post('/auth/login', { usernameOrEmail, password, rememberMe });
    return res.data;
  },

  /** UC-02: Đăng xuất hệ thống và thu hồi Refresh Token */
  logout: async () => {
    const res = await apiClient.post('/auth/logout', {});
    return res.data;
  },

  /** UC-04: Yêu cầu gửi mã/token khôi phục mật khẩu qua email công ty */
  forgotPassword: async (email) => {
    const res = await apiClient.post('/auth/forgot-password', { email });
    return res.data;
  },

  /** UC-05: Xác nhận đặt lại mật khẩu mới bằng token/OTP */
  resetPassword: async (payload) => {
    const res = await apiClient.post('/auth/reset-password', payload);
    return res.data;
  },

  /** Lấy hồ sơ người dùng hiện tại và danh sách đa vai trò */
  getMe: async () => {
    const res = await apiClient.get('/auth/me');
    return res.data;
  }
};

/**
 * 3. API Phân hệ Quản trị Hệ thống & Đa vai trò (Section III.3: System Administration - UC-06..UC-15)
 */
export const adminApi = {
  /** UC-06: Xem danh sách tài khoản nhân sự kèm bộ lọc tìm kiếm */
  getUsers: async (params = {}) => {
    const res = await apiClient.get('/users', { params });
    return res.data;
  },

  /** UC-08: Tạo tài khoản nhân sự mới (MOD-ADM-01) */
  createUser: async (payload) => {
    const res = await apiClient.post('/users', payload);
    return res.data;
  },

  /** UC-09: Cập nhật thông tin nhân sự và vai trò */
  updateUser: async (userId, payload) => {
    const res = await apiClient.put(`/users/${userId}`, payload);
    return res.data;
  },

  /** UC-10: Khóa (BANNED) hoặc Mở khóa (ACTIVE) tài khoản nhân sự */
  updateUserStatus: async (userId, status, reason) => {
    const res = await apiClient.patch(`/users/${userId}/status`, { status, reason });
    return res.data;
  },

  /** UC-11: Lấy danh sách vai trò và cấu hình quyền RBAC */
  getRoles: async () => {
    const res = await apiClient.get('/roles');
    return res.data;
  },

  /** UC-13: Truy vấn lịch sử nhật ký kiểm toán hệ thống bất biến (SCR-ADM-02) */
  getAuditLogs: async (params = {}) => {
    const res = await apiClient.get('/audit-logs', { params });
    return res.data;
  },

  /** UC-13 / SCR-ADM-02: Xuất nhật ký kiểm toán ra file CSV */
  exportAuditLogsCsvUrl: (params = {}) => {
    const query = new URLSearchParams(params).toString();
    return `${API_BASE_URL}/audit-logs/export-csv${query ? `?${query}` : ''}`;
  }
};

/**
 * 4. API Phân hệ Tiếp nhận Đơn hàng & Order Space (Section III.4: Order Intake - UC-30..UC-35)
 */
export const orderIntakeApi = {
  /** UC-34: Lấy danh sách đơn hàng bán chính thức và thống kê KPI (SCR-ORD-01) */
  getSalesOrders: async (params = {}) => {
    const res = await apiClient.get('/orders', { params });
    return res.data;
  },

  /** UC-30 & UC-31: Upload file Excel PO lên bảng Safe Excel PO Staging Grid (SCR-ORD-02) */
  uploadExcelPoToStaging: async (file, poNumber, customerId) => {
    const formData = new FormData();
    if (file) {
      formData.append('file', file);
    }
    if (poNumber) {
      formData.append('poNumber', poNumber);
    }
    if (customerId) {
      formData.append('customerId', customerId);
    }
    const res = await apiClient.post('/orders/staging/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    });
    return res.data;
  },

  /** UC-32: Kiểm tra lại toàn bộ các dòng trên bảng Staging Grid sau khi sửa inline (BR-ORD-02) */
  validateStagingGrid: async (payload) => {
    const res = await apiClient.post('/orders/staging/validate', payload);
    return res.data;
  },

  /** UC-33: Xác nhận đơn hàng đã sạch lỗi và tự động khởi tạo Order Space (BR-ORD-03) */
  confirmSalesOrder: async (payload) => {
    const res = await apiClient.post('/orders/confirm', payload);
    return res.data;
  },

  /** UC-35: Xem chi tiết Order Space Detail Dashboard (SCR-ORD-03) */
  getOrderSpaceDetail: async (spaceKeyOrId) => {
    const res = await apiClient.get(`/orders/spaces/${encodeURIComponent(spaceKeyOrId)}`);
    return res.data;
  }
};

export default apiClient;
