export type RoleCode = string;

export interface AuthUser {
  userId: string;
  username: string;
  email: string;
  fullName: string;
  phone: string;
  status: string;
  lastLoginAt: string;
  roles: RoleCode[];
  roleScopes: [
    {
      roleId: number;
      roleCode: RoleCode[];
      roleName: string;
      permissionsJson: string;
      assignedAt: string;
    },
  ];
}

// ---------- Requests (what you send) ----------
export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
  rememberMe: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  tokenOrOtp: string;
  newPassword: string;
  confirmNewPassword: string;
}

// ---------- Responses (what you get back) ----------
export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  user: AuthUser;
}

export type MeResponse = AuthUser;
