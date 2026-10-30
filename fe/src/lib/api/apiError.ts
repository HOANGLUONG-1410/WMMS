import axios from "axios";
import { HttpStatus } from "./httpStatus";

/**
 * Standard Backend Envelope from WMMS.Application.Common.Models.ApiResponse<T>
 */
export interface ApiResponseEnvelope<T = unknown> {
  success: boolean;
  messageCode?: string;
  message?: string;
  data?: T | null;
  errors?: string[] | Record<string, string[]> | null;
  timestamp?: string;
}

export class ApiError extends Error {
  status: number;          // HTTP status, 0 = network / no response
  messageCode?: string;    // e.g. MSG01..MSG13
  errors: string[];
  url?: string;

  constructor(
    message: string,
    status: number,
    messageCode?: string,
    errors: string[] = [],
    url?: string,
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.messageCode = messageCode;
    this.errors = errors;
    this.url = url;
  }

  /**
   * Normalizes any AxiosError or runtime error into a structured ApiError
   */
  static fromAxiosError(error: unknown): ApiError {
    if (error instanceof ApiError) {
      return error;
    }

    if (axios.isAxiosError<ApiResponseEnvelope>(error)) {
      const url = error.config?.url;

      // No response received (backend offline, CORS, timeout)
      if (!error.response) {
        return new ApiError(
          error.message || "Unable to connect to WMMS server.",
          HttpStatus.NETWORK_ERROR,
          "NETWORK_ERROR",
          [],
          url,
        );
      }

      const status = error.response.status;
      const payload = error.response.data;

      let normalizedErrors: string[] = [];
      if (Array.isArray(payload?.errors)) {
        normalizedErrors = payload.errors.map(String);
      } else if (payload?.errors && typeof payload.errors === "object") {
        normalizedErrors = Object.values(payload.errors)
          .flat()
          .map(String);
      }

      const rawMessage = payload?.message?.trim();
      const message =
        normalizedErrors.length > 0 &&
        (!rawMessage || rawMessage === "Validation failed.")
          ? normalizedErrors[0]
          : rawMessage || error.message || `Request failed with status ${status}`;

      return new ApiError(
        message,
        status,
        payload?.messageCode,
        normalizedErrors,
        url,
      );
    }

    if (error instanceof Error) {
      return new ApiError(error.message, HttpStatus.SERVER_ERROR, "UNKNOWN_ERROR");
    }

    return new ApiError(String(error), HttpStatus.SERVER_ERROR, "UNKNOWN_ERROR");
  }
}