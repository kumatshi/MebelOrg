using MebelOrg.Models;

namespace MebelOrg.Helpers;

public static class SessionManager
{
    public static UserAccount? CurrentUser { get; set; }

    public static UserRoleType Role => CurrentUser?.RoleType ?? UserRoleType.Guest;

    public static bool IsGuest => Role == UserRoleType.Guest;
    public static bool IsClient => Role == UserRoleType.Client;
    public static bool CanFilterFurniture => Role is UserRoleType.Manager or UserRoleType.Admin;
    public static bool CanManageFurniture => Role == UserRoleType.Admin;
    public static bool CanViewOrders => Role is UserRoleType.Manager or UserRoleType.Admin;
    public static bool CanManageOrders => Role == UserRoleType.Admin;
}