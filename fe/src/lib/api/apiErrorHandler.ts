import { toast } from "sonner";
import { ApiError } from "./apiError";
import { HttpStatus } from "./httpStatus";
import { navigate } from "../navigation";
import { setAccessToken } from "../../features/auth/store/tokenStore";

type ErrorAction = (error: ApiError) => void;

const handlers: Partial<Record<number, ErrorAction>> = {
  [HttpStatus.NETWORK_ERROR]: (e) =>
    toast.error("Lỗi mạng", { description: e.message, id: "network" }),

  [HttpStatus.BAD_REQUEST]: (e) =>
    toast.error("Lỗi dữ liệu không hợp lệ", {
      description: e.errors[0] || e.message,
      id: "400",
    }),

  [HttpStatus.UNAUTHORIZED]: (e) => {
    // Không bắn toast pop-up cho API đăng nhập (lỗi được hiển thị trực tiếp trên form)
    if (e.url?.includes("/auth/login")) {
      return;
    }

    setAccessToken(null);
    toast.error("Phiên đăng nhập đã hết hạn", {
      description: e.message || "Vui lòng đăng nhập lại.",
      id: "401",
    });
    if (window.location.pathname !== "/login") {
      navigate("/login");
    }
  },

  [HttpStatus.FORBIDDEN]: (e) =>
    toast.error("Không có quyền truy cập", { description: e.message, id: "403" }),

  [HttpStatus.NOT_FOUND]: (e) =>
    toast.error("Không tìm thấy", { description: e.message, id: "404" }),

  [HttpStatus.METHOD_NOT_ALLOWED]: (e) =>
    toast.error("Phương thức không được phép", { description: e.message, id: "405" }),

  [HttpStatus.CONFLICT]: (e) =>
    toast.error("Xung đột dữ liệu", {
      description: e.errors[0] || e.message,
      id: "409",
    }),

  [HttpStatus.UNPROCESSABLE]: (e) =>
    toast.error("Dữ liệu không thể xử lý", {
      description: e.errors[0] || e.message,
      id: "422",
    }),

  [HttpStatus.LOCKED]: (e) =>
    toast.error("Tài khoản tạm thời bị khóa", {
      description: e.message,
      id: "423",
    }),

  [HttpStatus.TOO_MANY_REQUESTS]: (e) =>
    toast.error("Quá nhiều yêu cầu", {
      description: e.message || "Vui lòng đợi một lúc rồi thử lại.",
      id: "429",
    }),
};

const fallback: ErrorAction = (e) =>
  toast.error(e.status >= 500 ? "Lỗi máy chủ" : "Yêu cầu thất bại", {
    description: e.message,
    id: `err-${e.status}`,
  });

export class ApiErrorHandler {
  static handle(error: unknown) {
    const apiError =
      error instanceof ApiError ? error : ApiError.fromAxiosError(error);
    (handlers[apiError.status] ?? fallback)(apiError);
  }
}
