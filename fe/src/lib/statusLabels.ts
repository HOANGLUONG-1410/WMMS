/**
 * Nhãn tiếng Việt hiển thị trên giao diện cho các mã trạng thái / loại hành động do backend trả về.
 * Chỉ dùng để HIỂN THỊ: giá trị gốc (mã) vẫn được giữ nguyên để so sánh logic và gọi API.
 */

const pick = (map: Record<string, string>, code?: string | null, fallback = ""): string => {
  const key = String(code ?? "").trim().toUpperCase();
  if (!key) return fallback;
  return map[key] ?? String(code);
};

const USER_STATUS: Record<string, string> = {
  ACTIVE: "Hoạt động",
  INACTIVE: "Ngừng hoạt động",
  BANNED: "Bị cấm",
  LOCKED: "Bị khóa",
  SUSPENDED: "Tạm khóa",
};

const ORDER_LIFECYCLE: Record<string, string> = {
  NEWLY_RECEIVED: "Mới nhận",
  BOM_SETTING: "Đang lập BOM",
  IN_PRODUCTION: "Đang sản xuất",
  COMPLETED: "Hoàn thành",
  CANCELLED: "Đã hủy",
  CONFIRMED: "Đã xác nhận",
  IN_PLANNING: "Đang lập kế hoạch",
};

const BOM_STATUS: Record<string, string> = {
  PENDING_BOM: "Chờ lập BOM",
  BOM_DRAFT: "BOM nháp",
  DRAFT: "Nháp",
  WAITING_MAT: "Chờ vật tư",
  WAITING_FOR_MATERIALS: "Chờ vật tư",
  READY_PROD: "Sẵn sàng sản xuất",
  READY_FOR_PRODUCTION: "Sẵn sàng sản xuất",
  IN_PROD: "Đang sản xuất",
  DONE: "Hoàn tất",
};

const AUDIT_ACTION: Record<string, string> = {
  AUTH_LOGIN_SUCCESS: "Đăng nhập thành công",
  AUTH_LOGIN_FAILED: "Đăng nhập thất bại",
  AUTH_ACCOUNT_LOCKED: "Khóa tài khoản",
  AUTH_ACCOUNT_AUTO_UNLOCKED: "Tự động mở khóa tài khoản",
  AUTH_LOGOUT: "Đăng xuất",
  AUTH_TOKEN_REFRESHED: "Làm mới phiên đăng nhập",
  AUTH_PASSWORD_CHANGED: "Đổi mật khẩu",
  AUTH_PASSWORD_RESET_REQUESTED: "Yêu cầu đặt lại mật khẩu",
  AUTH_PASSWORD_RESET_CONFIRMED: "Xác nhận đặt lại mật khẩu",
  USER_CREATED: "Tạo người dùng",
  USER_UPDATED: "Cập nhật người dùng",
  USER_ROLES_UPDATED: "Cập nhật vai trò người dùng",
  USER_STATUS_CHANGED: "Đổi trạng thái người dùng",
  ROLE_PERMISSIONS_UPDATED: "Cập nhật phân quyền vai trò",
  SYSTEM_CONFIG_UPDATED: "Cập nhật cấu hình hệ thống",
  UPLOAD_PO_STAGING: "Tải PO lên Staging",
  CONFIRM_PO_STAGING: "Xác nhận PO Staging",
  CREATE_ORDER_SPACE: "Tạo Order Space",
  UPDATE_ORDER_SPACE_DRAWING: "Cập nhật bản vẽ Order Space",
  BOM_CREATED: "Tạo BOM",
  BOM_UPDATED: "Cập nhật BOM",
  BOM_APPROVED: "Duyệt BOM",
  AUTHORIZE_OVERDRIVER: "Cho phép vượt chốt (Overdriver)",
  APPROVE_SCRAP_REPLACEMENT: "Duyệt sản xuất bù phế phẩm",
  COMMIT_GOODS_RECEIPT: "Xác nhận nhập kho",
  COMMIT_GRN: "Xác nhận phiếu nhập kho",
  APPROVE_CONSOLIDATED_PR: "Duyệt PR gộp",
  CONSOLIDATED_PR_APPROVED: "Duyệt PR gộp",
  CONSOLIDATED_PR_REJECTED: "Từ chối PR gộp",
  PURCHASE_ORDER_ISSUED: "Phát hành PO",
  PURCHASE_ORDER_UPDATED: "Cập nhật PO",
  INVENTORY_AUTO_ALLOCATED: "Tự động phân bổ tồn kho",
  INVENTORY_REALLOCATED_BY_ETD: "Phân bổ lại tồn kho theo ETD",
  INVENTORY_ALLOCATION_RELEASED: "Giải phóng giữ chỗ tồn kho",
};

export const userStatusLabel = (code?: string | null) => pick(USER_STATUS, code, "Tạm khóa");
export const lifecycleStatusLabel = (code?: string | null) => pick(ORDER_LIFECYCLE, code);
export const bomStatusLabel = (code?: string | null) => pick(BOM_STATUS, code);
export const auditActionLabel = (code?: string | null) => pick(AUDIT_ACTION, code);
