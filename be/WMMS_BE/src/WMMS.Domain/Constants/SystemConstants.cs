namespace WMMS.Domain.Constants;

/// <summary>
/// Các mã vai trò hệ thống (System Roles) hỗ trợ cơ chế Multi-Role RBAC (GBR-04).
/// Một tài khoản có thể sở hữu 1..* vai trò thông qua bảng liên kết <c>user_roles</c>.
/// </summary>
public static class RoleConstants
{
    public const string Admin = "ADMIN";
    public const string Owner = "OWNER";
    public const string Sales = "SALES";
    public const string Planner = "PLANNER";
    public const string Purchasing = "PURCHASING";
    public const string Warehouse = "WAREHOUSE";
    public const string Leader = "LEADER";
    public const string Qc = "QC";

    public static readonly IReadOnlyList<string> AllRoles = new[]
    {
        Admin, Owner, Sales, Planner, Purchasing, Warehouse, Leader, Qc
    };
}

/// <summary>
/// Chuẩn hóa mã thông báo hệ thống (System Message Codes MSG01 - MSG13) theo đặc tả Report 3 (Mục 3.1.4).
/// </summary>
public static class MessageCodes
{
    /// <summary>MSG01: Trường bắt buộc bị bỏ trống.</summary>
    public const string Msg01RequiredField = "MSG01";

    /// <summary>MSG02: Sai tên đăng nhập/email hoặc mật khẩu (UC-01).</summary>
    public const string Msg02InvalidCredentials = "MSG02";

    /// <summary>MSG03: Tài khoản tạm thời bị khóa 30 phút sau 5 lần đăng nhập sai liên tiếp (UC-01, BR-SEC-02).</summary>
    public const string Msg03AccountLocked = "MSG03";

    /// <summary>MSG04: Tài khoản đã bị vô hiệu hóa hoặc bị cấm bởi Quản trị viên (UC-01, UC-10).</summary>
    public const string Msg04AccountBannedOrInactive = "MSG04";

    /// <summary>MSG05: Thông báo chung khi yêu cầu đặt lại mật khẩu để chống dò quét email (UC-04).</summary>
    public const string Msg05PasswordResetDispatched = "MSG05";

    /// <summary>MSG06: Liên kết hoặc mã OTP đặt lại mật khẩu không hợp lệ hoặc đã hết hạn 15 phút (UC-05).</summary>
    public const string Msg06ResetTokenInvalidOrExpired = "MSG06";

    /// <summary>MSG07: Thay đổi thông tin / tạo mới thành công.</summary>
    public const string Msg07Success = "MSG07";

    /// <summary>MSG08: Xung đột dữ liệu trùng lặp (Username / Email đã tồn tại - HTTP 409 Conflict, UC-08).</summary>
    public const string Msg08DuplicateConflict = "MSG08";

    /// <summary>MSG09: Vượt giới hạn tần suất yêu cầu đặt lại mật khẩu (tối đa 3 lần / 10 phút, UC-04).</summary>
    public const string Msg09RateLimitExceeded = "MSG09";

    /// <summary>MSG10: Không tìm thấy bản ghi yêu cầu (HTTP 404 Not Found).</summary>
    public const string Msg10NotFound = "MSG10";

    /// <summary>MSG11: Vi phạm quy tắc quản trị hoặc không đủ quyền hạn (BR-ADM-01, BR-ADM-02, BR-ADM-03).</summary>
    public const string Msg11ForbiddenRule = "MSG11";

    /// <summary>MSG12: Phiên đăng nhập hoặc Refresh Token không hợp lệ / đã bị thu hồi (BR-SEC-01).</summary>
    public const string Msg12InvalidToken = "MSG12";

    /// <summary>MSG13: Lỗi đầu vào hoặc vi phạm độ phức tạp mật khẩu (UC-03, UC-05).</summary>
    public const string Msg13ValidationError = "MSG13";
}

/// <summary>
/// Hằng số các loại hành động ghi nhận vào bảng nhật ký kiểm toán <c>audit_logs</c> (GBR-05, UC-13, UC-14).
/// </summary>
public static class AuditActionConstants
{
    public const string LoginSuccess = "AUTH_LOGIN_SUCCESS";
    public const string LoginFailed = "AUTH_LOGIN_FAILED";
    public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string AccountAutoUnlocked = "AUTH_ACCOUNT_AUTO_UNLOCKED";
    public const string Logout = "AUTH_LOGOUT";
    public const string TokenRefreshed = "AUTH_TOKEN_REFRESHED";
    public const string PasswordChanged = "AUTH_PASSWORD_CHANGED";
    public const string PasswordResetRequested = "AUTH_PASSWORD_RESET_REQUESTED";
    public const string PasswordResetConfirmed = "AUTH_PASSWORD_RESET_CONFIRMED";

    public const string UserCreated = "USER_CREATED";
    public const string UserUpdated = "USER_UPDATED";
    public const string UserRolesUpdated = "USER_ROLES_UPDATED";
    public const string UserStatusChanged = "USER_STATUS_CHANGED";
    public const string RolePermissionsUpdated = "ROLE_PERMISSIONS_UPDATED";
}

public static class DefectCodeConstants
{
    public const string CutErr01 = "ERR_CUT_01";
    public const string WoodErr01 = "ERR_WOOD_01";
    public const string SurfErr02 = "ERR_SURF_02";
    public const string AssyErr03 = "ERR_ASSY_03";
    public const string PaintErr04 = "ERR_PAINT_04";
}

public static class SystemConfigKeys
{
    public const string DefaultScrapMin = "DEFAULT_SCRAP_MIN";
    public const string DefaultScrapMax = "DEFAULT_SCRAP_MAX";
    public const string EtdWarningDays = "ETD_WARNING_DAYS";
}
