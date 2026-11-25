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
  ACTIVE: "Đang hoạt động",
  INACTIVE: "Ngừng hoạt động",
  BANNED: "Đã khóa vĩnh viễn",
  LOCKED: "Tạm khóa đăng nhập",
  SUSPENDED: "Tạm khóa đăng nhập",
};

const ORDER_LIFECYCLE: Record<string, string> = {
  NEWLY_RECEIVED: "Mới tiếp nhận",
  BOM_SETTING: "Đang thiết lập định mức",
  WAITING_FOR_MATERIAL: "Đang chờ vật tư",
  READY_FOR_PRODUCTION: "Sẵn sàng sản xuất",
  IN_PRODUCTION: "Đang sản xuất",
  COMPLETED: "Đã hoàn thành",
  CANCELLED: "Đã hủy",
  CONFIRMED: "Đã xác nhận",
  IN_PLANNING: "Đang lập kế hoạch",
};

const BOM_STATUS: Record<string, string> = {
  PENDING_BOM: "Chờ thiết lập định mức",
  BOM_DRAFT: "Bản nháp định mức",
  DRAFT: "Bản nháp định mức",
  PENDING_APPROVAL: "Chờ phê duyệt định mức",
  APPROVED: "Đã duyệt định mức",
  WAITING_MAT: "Chưa đủ nguyên liệu",
  WAITING_FOR_MATERIALS: "Chưa đủ nguyên liệu",
  WAITING_FOR_MATERIAL: "Chưa đủ nguyên liệu",
  READY_PROD: "Đủ vật tư - Sẵn sàng SX",
  READY_FOR_PRODUCTION: "Đủ vật tư - Sẵn sàng SX",
  IN_PROD: "Đang sản xuất",
  IN_PRODUCTION: "Đang sản xuất",
  DONE: "Đã hoàn thành",
  COMPLETED: "Đã hoàn thành",
};

const PR_STATUS: Record<string, string> = {
  PENDING_APPROVAL: "Chờ Chủ xưởng phê duyệt",
  APPROVED: "Đã phê duyệt",
  REJECTED: "Từ chối",
  PO_ISSUED: "Đã lập đơn mua",
};

const PO_STATUS: Record<string, string> = {
  ISSUED: "Đã phát hành",
  PARTIALLY_RECEIVED: "Đã nhận một phần",
  RECEIVED: "Đã nhận đủ hàng",
  FULFILLED: "Đã nhận đủ hàng",
  COMPLETED: "Đã nhận đủ hàng",
  CANCELLED: "Đã hủy",
};

const ALLOCATION_STATUS: Record<string, string> = {
  RESERVED: "Đã giữ chỗ",
  SUFFICIENT: "Đủ tồn kho",
  FULL: "Đã giữ chỗ",
  FULLY_ALLOCATED: "Đã giữ chỗ",
  PARTIAL: "Chưa đủ nguyên liệu",
  PARTIALLY_ALLOCATED: "Chưa đủ nguyên liệu",
  PARTIALLY_RESERVED: "Chưa đủ nguyên liệu",
  PARTIAL_SHORTAGE: "Chưa đủ nguyên liệu",
  CRITICAL_SHORTAGE: "Chưa đủ nguyên liệu",
  SHORTAGE: "Chưa đủ nguyên liệu",
  WAITING: "Chưa đủ nguyên liệu",
  WAITING_MATERIAL: "Chưa đủ nguyên liệu",
  PENDING: "Chờ phân bổ",
  ISSUED: "Đã xuất kho sản xuất",
  ISSUED_TO_SHOP: "Đã xuất kho sản xuất",
  RELEASED: "Đã giải phóng giữ chỗ",
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
  UPLOAD_PO_STAGING: "Tải đơn hàng Excel",
  CONFIRM_PO_STAGING: "Xác nhận đơn hàng Excel",
  CREATE_ORDER_SPACE: "Tạo không gian đơn hàng",
  UPDATE_ORDER_SPACE_DRAWING: "Cập nhật bản vẽ đơn hàng",
  BOM_CREATED: "Tạo định mức vật tư",
  BOM_UPDATED: "Cập nhật định mức vật tư",
  BOM_APPROVED: "Duyệt định mức vật tư",
  AUTHORIZE_OVERDRIVER: "Cho phép chạy vượt chốt",
  APPROVE_SCRAP_REPLACEMENT: "Duyệt sản xuất bù phế phẩm",
  COMMIT_GOODS_RECEIPT: "Xác nhận nhập kho",
  COMMIT_GRN: "Xác nhận phiếu nhập kho",
  APPROVE_CONSOLIDATED_PR: "Duyệt yêu cầu mua tổng hợp",
  CONSOLIDATED_PR_APPROVED: "Duyệt yêu cầu mua tổng hợp",
  CONSOLIDATED_PR_REJECTED: "Từ chối yêu cầu mua tổng hợp",
  PURCHASE_ORDER_ISSUED: "Phát hành đơn mua hàng",
  PURCHASE_ORDER_UPDATED: "Cập nhật đơn mua hàng",
  INVENTORY_AUTO_ALLOCATED: "Tự động phân bổ tồn kho",
  INVENTORY_REALLOCATED_BY_ETD: "Phân bổ lại tồn kho theo ngày giao",
  INVENTORY_ALLOCATION_RELEASED: "Giải phóng giữ chỗ tồn kho",
};

export const userStatusLabel = (code?: string | null) => pick(USER_STATUS, code, "Tạm khóa");
export const lifecycleStatusLabel = (code?: string | null) => pick(ORDER_LIFECYCLE, code);
export const bomStatusLabel = (code?: string | null) => pick(BOM_STATUS, code);
export const prStatusLabel = (code?: string | null) => pick(PR_STATUS, code);
export const poStatusLabel = (code?: string | null) => pick(PO_STATUS, code);
export const allocationStatusLabel = (code?: string | null) => pick(ALLOCATION_STATUS, code);
export const auditActionLabel = (code?: string | null) => pick(AUDIT_ACTION, code);
