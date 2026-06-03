namespace MebelOrg.Models;

public class UserAccount
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public UserRoleType RoleType { get; set; }

    public static UserRoleType ParseRole(string roleName) => roleName switch
    {
        "Администратор" => UserRoleType.Admin,
        "Менеджер" => UserRoleType.Manager,
        "Авторизованный клиент" => UserRoleType.Client,
        _ => UserRoleType.Guest
    };
}