using FluentAssertions;
using Microsoft.Extensions.Options;
using NMS.Application.Auth;
using NMS.Application.Rooms;
using NMS.Application.Security;
using NMS.Application.Students;
using NMS.Application.Users;
using NMS.Domain.Common;
using NMS.Domain.Identity;
using Xunit;

namespace NMS.UnitTests.Application;

public class RolePermissionTests
{
    [Fact]
    public void Super_admin_holds_every_permission() =>
        RolePermissions.ForRoles([SystemRoles.SuperAdmin])
            .Should().BeEquivalentTo(RolePermissions.AllPermissionNames());

    [Theory]
    [InlineData(SystemRoles.Parent, Permissions.StudentsWrite)]
    [InlineData(SystemRoles.Parent, Permissions.StaffRead)]
    [InlineData(SystemRoles.Parent, Permissions.CentreStructureRead)]
    [InlineData(SystemRoles.Parent, Permissions.UserManagementManage)]
    [InlineData(SystemRoles.Teacher, Permissions.StudentsWrite)]
    [InlineData(SystemRoles.Teacher, Permissions.StaffWrite)]
    [InlineData(SystemRoles.Teacher, Permissions.BillingRead)]
    [InlineData(SystemRoles.Receptionist, Permissions.StudentsWrite)]
    [InlineData(SystemRoles.Accounts, Permissions.StudentsRead)]
    [InlineData(SystemRoles.Marketing, Permissions.StudentsRead)]
    [InlineData(SystemRoles.CentreAdmin, Permissions.AuditLogView)]
    [InlineData(SystemRoles.CentreAdmin, Permissions.UserManagementManage)]
    public void Role_does_not_hold_permission(string role, string permission) =>
        RolePermissions.ForRoles([role]).Should().NotContain(permission);

    [Theory]
    [InlineData(SystemRoles.Parent, Permissions.StudentsRead)]
    [InlineData(SystemRoles.Teacher, Permissions.StudentsRead)]
    [InlineData(SystemRoles.Teacher, Permissions.CentreStructureRead)]
    [InlineData(SystemRoles.CentreAdmin, Permissions.StudentsWrite)]
    [InlineData(SystemRoles.CentreAdmin, Permissions.CentreStructureWrite)]
    [InlineData(SystemRoles.It, Permissions.AuditLogView)]
    [InlineData(SystemRoles.Receptionist, Permissions.EnquiriesWrite)]
    public void Role_holds_permission(string role, string permission) =>
        RolePermissions.ForRoles([role]).Should().Contain(permission);

    [Fact]
    public void Unknown_role_has_no_permissions() =>
        RolePermissions.ForRoles(["intruder"]).Should().BeEmpty();

    [Fact]
    public void Permissions_combine_across_roles() =>
        RolePermissions.ForRoles([SystemRoles.Teacher, SystemRoles.Accounts])
            .Should().Contain([Permissions.AttendanceWrite, Permissions.BillingWrite]);
}

public class ValidationTests
{
    private static readonly IOptions<AuthOptions> Auth = Options.Create(new AuthOptions { MinPasswordLength = 12 });

    private static StudentFields Student(
        string first = "Mariam", string last = "Hassan", DateOnly? dob = null, string? gender = null) =>
        new(first, last, null, null, dob ?? new DateOnly(2023, 5, 1), gender, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null);

    [Fact]
    public void Valid_student_passes() =>
        new CreateStudentValidator().Validate(new CreateStudentCommand(Student(), null)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("", "Hassan")]
    [InlineData("Mariam", "")]
    public void Student_requires_both_names(string first, string last) =>
        new CreateStudentValidator().Validate(new CreateStudentCommand(Student(first, last), null)).IsValid.Should().BeFalse();

    [Fact]
    public void Student_cannot_be_born_in_the_future() =>
        new CreateStudentValidator()
            .Validate(new CreateStudentCommand(Student(dob: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))), null))
            .IsValid.Should().BeFalse();

    [Fact]
    public void Student_gender_must_be_known() =>
        new CreateStudentValidator().Validate(new CreateStudentCommand(Student(gender: "robot"), null)).IsValid.Should().BeFalse();

    [Fact]
    public void Guardian_email_must_be_valid()
    {
        var guardian = new NewGuardianInput("Aisha", "Hassan", "not-an-email", null, "mother");

        new CreateStudentValidator().Validate(new CreateStudentCommand(Student(), [guardian])).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(10, 0, 1, 4, true)]
    [InlineData(0, 0, 1, 4, false)]
    [InlineData(10, 24, 12, 4, false)]
    [InlineData(10, 0, 1, 0, false)]
    public void Room_rules(int capacity, int minAge, int maxAge, int ratio, bool valid)
    {
        var command = new CreateRoomCommand(new Dictionary<string, string> { ["en"] = "Toddlers" }, capacity, minAge, maxAge, ratio);

        new CreateRoomValidator().Validate(command).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Room_requires_an_english_name()
    {
        var command = new CreateRoomCommand(new Dictionary<string, string> { ["ar"] = "غرفة" }, 10, 0, 12, 4);

        new CreateRoomValidator().Validate(command).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Str0ngPassw0rd", true)]
    [InlineData("short1", false)]
    [InlineData("nodigitshereatall", false)]
    [InlineData("123456789012", false)]
    public void Password_policy(string password, bool valid)
    {
        var command = new CreateUserCommand("a@b.ae", "A", "B", password, [SystemRoles.Teacher], null, null, null);

        new CreateUserValidator(Auth).Validate(command).IsValid.Should().Be(valid);
    }

    [Fact]
    public void User_needs_at_least_one_role()
    {
        var command = new CreateUserCommand("a@b.ae", "A", "B", "Str0ngPassw0rd", [], null, null, null);

        new CreateUserValidator(Auth).Validate(command).IsValid.Should().BeFalse();
    }
}

public class SensitiveFieldTests
{
    [Fact]
    public void Medical_and_identity_fields_are_marked_sensitive()
    {
        var sensitive = typeof(NMS.Domain.CentreManagement.Student).GetProperties()
            .Where(p => Attribute.IsDefined(p, typeof(SensitiveAttribute)))
            .Select(p => p.Name);

        sensitive.Should().BeEquivalentTo(
            "MedicalNotes", "AllergyInfo", "DietaryRequirements", "CustodyNotes", "PassportNumber", "EmiratesId");
    }
}
