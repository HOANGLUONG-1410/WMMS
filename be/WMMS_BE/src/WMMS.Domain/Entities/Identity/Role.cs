namespace WMMS.Domain.Entities.Identity;

public class Role
{
    public int RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = "{}";

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
