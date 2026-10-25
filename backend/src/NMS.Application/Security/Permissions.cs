using NMS.Domain.Identity;

namespace NMS.Application.Security;

/// <summary>Permission names use the format &lt;module&gt;.&lt;action&gt; (spec 09 §2.2).</summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";
    public const string StudentsRead = "students.read";
    public const string StudentsWrite = "students.write";
    public const string StudentsDelete = "students.delete";
    public const string StaffRead = "staff.read";
    public const string StaffWrite = "staff.write";
    public const string AttendanceRead = "attendance.read";
    public const string AttendanceWrite = "attendance.write";
    public const string AttendanceOverride = "attendance.override";
    public const string ActivitiesRead = "activities.read";
    public const string ActivitiesWrite = "activities.write";
    public const string ActivitiesApprove = "activities.approve";
    public const string MessagingRead = "messaging.read";
    public const string MessagingWrite = "messaging.write";
    public const string MessagingBroadcast = "messaging.broadcast";
    public const string BillingRead = "billing.read";
    public const string BillingWrite = "billing.write";
    public const string InvoicesGenerate = "invoices.generate";
    public const string PaymentsRecord = "payments.record";
    public const string PaymentsRefund = "payments.refund";
    public const string EnquiriesRead = "enquiries.read";
    public const string EnquiriesWrite = "enquiries.write";
    public const string ToursRead = "tours.read";
    public const string ToursWrite = "tours.write";
    public const string CalendarRead = "calendar.read";
    public const string CalendarWrite = "calendar.write";
    public const string FormsRead = "forms.read";
    public const string FormsWrite = "forms.write";
    public const string ToolsUse = "tools.use";
    public const string ReportsView = "reports.view";
    public const string ReportsGenerate = "reports.generate";
    public const string SettingsRead = "settings.read";
    public const string SettingsWrite = "settings.write";
    public const string AuditLogView = "audit_log.view";
    public const string UserManagementManage = "user_management.manage";
    public const string NotificationsPreferences = "notifications.preferences";
    public const string ResourcesRead = "resources.read";
    public const string ResourcesWrite = "resources.write";
    public const string LearningRead = "learning.read";
    public const string LearningWrite = "learning.write";
    public const string LearningApprove = "learning.approve";
    public const string JourneyRead = "journey.read";
    public const string GroupRead = "group.read";
    public const string GroupManage = "group.manage";

    /// <summary>Rooms, programs, groups and labels are readable by any staff role (spec 08 §15.4 "Staff+").</summary>
    public const string CentreStructureRead = "centre_structure.read";

    public const string CentreStructureWrite = "centre_structure.write";

    /// <summary>Free questions to the AI Brain about the centre's data ("chat with data"); the super administrator only.</summary>
    public const string AiChat = "ai.chat";
}

/// <summary>The RBAC matrix from spec 09 §2.2 expressed as code. Scoping (room, own child) is enforced in handlers.</summary>
public static class RolePermissions
{
    private static readonly string[] Everyone = [Permissions.DashboardView, Permissions.NotificationsPreferences];

    private static readonly Dictionary<string, string[]> Map = new()
    {
        [SystemRoles.SuperAdmin] = [.. AllPermissions()],
        [SystemRoles.It] = [.. AllPermissions().Where(p => p != Permissions.AiChat)],
        [SystemRoles.CentreAdmin] =
        [
            .. Everyone,
            Permissions.StudentsRead, Permissions.StudentsWrite, Permissions.StudentsDelete,
            Permissions.StaffRead, Permissions.StaffWrite,
            Permissions.AttendanceRead, Permissions.AttendanceWrite, Permissions.AttendanceOverride,
            Permissions.ActivitiesRead, Permissions.ActivitiesWrite, Permissions.ActivitiesApprove,
            Permissions.MessagingRead, Permissions.MessagingWrite, Permissions.MessagingBroadcast,
            Permissions.BillingRead, Permissions.BillingWrite, Permissions.InvoicesGenerate, Permissions.PaymentsRecord, Permissions.PaymentsRefund,
            Permissions.EnquiriesRead, Permissions.EnquiriesWrite, Permissions.ToursRead, Permissions.ToursWrite,
            Permissions.CalendarRead, Permissions.CalendarWrite, Permissions.FormsRead, Permissions.FormsWrite,
            Permissions.ToolsUse, Permissions.ReportsView, Permissions.ReportsGenerate,
            Permissions.SettingsRead, Permissions.SettingsWrite,
            Permissions.CentreStructureRead, Permissions.CentreStructureWrite,
            Permissions.ResourcesRead, Permissions.ResourcesWrite,
            Permissions.LearningRead, Permissions.LearningWrite, Permissions.LearningApprove, Permissions.JourneyRead,
        ],
        [SystemRoles.Teacher] =
        [
            .. Everyone,
            Permissions.StudentsRead, Permissions.StaffRead,
            Permissions.AttendanceRead, Permissions.AttendanceWrite,
            Permissions.ActivitiesRead, Permissions.ActivitiesWrite,
            Permissions.MessagingRead, Permissions.MessagingWrite,
            Permissions.CalendarRead, Permissions.FormsRead, Permissions.ReportsView,
            Permissions.CentreStructureRead, Permissions.ResourcesRead,
            Permissions.LearningRead, Permissions.LearningWrite, Permissions.JourneyRead,
        ],
        [SystemRoles.Parent] =
        [
            .. Everyone,
            Permissions.StudentsRead, Permissions.AttendanceRead, Permissions.ActivitiesRead,
            Permissions.MessagingRead, Permissions.MessagingWrite, Permissions.BillingRead,
            Permissions.CalendarRead, Permissions.FormsRead, Permissions.ResourcesRead, Permissions.JourneyRead,
        ],
        [SystemRoles.Receptionist] =
        [
            .. Everyone,
            Permissions.StudentsRead, Permissions.StaffRead,
            Permissions.AttendanceRead, Permissions.AttendanceWrite,
            Permissions.MessagingRead, Permissions.MessagingWrite,
            Permissions.EnquiriesRead, Permissions.EnquiriesWrite, Permissions.ToursRead, Permissions.ToursWrite,
            Permissions.CalendarRead, Permissions.CalendarWrite, Permissions.FormsRead, Permissions.FormsWrite,
            Permissions.ReportsView, Permissions.CentreStructureRead, Permissions.ResourcesRead, Permissions.ResourcesWrite,
        ],
        [SystemRoles.Accounts] =
        [
            .. Everyone,
            Permissions.StaffRead,
            Permissions.BillingRead, Permissions.BillingWrite, Permissions.InvoicesGenerate, Permissions.PaymentsRecord, Permissions.PaymentsRefund,
            Permissions.ReportsView, Permissions.ReportsGenerate, Permissions.CentreStructureRead, Permissions.ResourcesRead,
        ],
        [SystemRoles.Marketing] =
        [
            .. Everyone,
            Permissions.MessagingRead, Permissions.MessagingWrite, Permissions.MessagingBroadcast,
            Permissions.EnquiriesRead, Permissions.EnquiriesWrite,
            Permissions.CalendarRead, Permissions.CalendarWrite, Permissions.ReportsView, Permissions.ResourcesRead, Permissions.ResourcesWrite,
        ],
    };

    public static IReadOnlyList<string> AllRoleNames() => Map.Keys.ToList();

    public static IReadOnlySet<string> ForRoles(IEnumerable<string> roles)
    {
        var result = new HashSet<string>();
        foreach (var role in roles)
        {
            if (Map.TryGetValue(role, out var permissions))
            {
                result.UnionWith(permissions);
            }
        }

        return result;
    }

    public static IReadOnlyList<string> AllPermissionNames() => AllPermissions();

    private static string[] AllPermissions() =>
        typeof(Permissions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();
}
