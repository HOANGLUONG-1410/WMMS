import { toast } from "sonner";
import { ApiError } from "./apiError";
import { HttpStatus } from "./httpStatus";
import { navigate } from "../navigation";
import { setAccessToken } from "../../features/auth/store/tokenStore";

type ErrorAction = (error: ApiError) => void;

const handlers: Partial<Record<number, ErrorAction>> = {
  [HttpStatus.NETWORK_ERROR]: (e) =>
    toast.error("Network error", { description: e.message, id: "network" }),

  [HttpStatus.BAD_REQUEST]: (e) =>
    toast.error("Validation error", {
      description: e.errors[0] || e.message,
      id: "400",
    }),

  [HttpStatus.UNAUTHORIZED]: (e) => {
    // Distinguish wrong login credentials (MSG02) from an expired JWT session (MSG12)
    if (e.messageCode === "MSG02" || e.url?.includes("/auth/login")) {
      toast.error("Authentication failed", {
        description: e.message,
        id: "401-login",
      });
      return;
    }

    setAccessToken(null);
    toast.error("Session expired", {
      description: e.message || "Please sign in again.",
      id: "401",
    });
    if (window.location.pathname !== "/login") {
      navigate("/login");
    }
  },

  [HttpStatus.FORBIDDEN]: (e) =>
    toast.error(
      e.messageCode === "MSG04" ? "Account restricted" : "Access denied",
      { description: e.message, id: "403" },
    ),

  [HttpStatus.NOT_FOUND]: (e) =>
    toast.error("Not found", { description: e.message, id: "404" }),

  [HttpStatus.METHOD_NOT_ALLOWED]: (e) =>
    toast.error("Method not allowed", { description: e.message, id: "405" }),

  [HttpStatus.CONFLICT]: (e) =>
    toast.error("Data conflict", {
      description: e.errors[0] || e.message,
      id: "409",
    }),

  [HttpStatus.UNPROCESSABLE]: (e) =>
    toast.error("Unprocessable entity", {
      description: e.errors[0] || e.message,
      id: "422",
    }),

  [HttpStatus.LOCKED]: (e) =>
    toast.error("Account temporarily locked", {
      description: e.message,
      id: "423",
    }),

  [HttpStatus.TOO_MANY_REQUESTS]: (e) =>
    toast.error("Too many requests", {
      description: e.message || "Please wait before trying again.",
      id: "429",
    }),
};

const fallback: ErrorAction = (e) =>
  toast.error(e.status >= 500 ? "Server error" : "Request failed", {
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
