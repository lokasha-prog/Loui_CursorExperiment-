using FluentAssertions;
using NMS.Domain.CentreManagement;
using NMS.Domain.Identity;
using NMS.Shared.Paging;
using NMS.Shared.Results;
using Xunit;

namespace NMS.UnitTests.Domain;

public class StudentStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(StudentStatuses.SignedUp, StudentStatuses.Active)]
    [InlineData(StudentStatuses.Active, StudentStatuses.OnHold)]
    [InlineData(StudentStatuses.OnHold, StudentStatuses.Active)]
    [InlineData(StudentStatuses.Active, StudentStatuses.Withdrawn)]
    [InlineData(StudentStatuses.Active, StudentStatuses.Transferred)]
    public void Allowed_transitions_succeed(string from, string to)
    {
        // Arrange
        var student = new Student { Status = from };

        // Act
        var result = student.TransitionTo(to, "reason", Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        student.Status.Should().Be(to);
        student.StatusChangedAt.Should().Be(Now);
        student.StatusReason.Should().Be("reason");
    }

    [Theory]
    [InlineData(StudentStatuses.Withdrawn, StudentStatuses.Active)]
    [InlineData(StudentStatuses.Transferred, StudentStatuses.Active)]
    [InlineData(StudentStatuses.SignedUp, StudentStatuses.OnHold)]
    public void Forbidden_transitions_fail_without_changing_the_student(string from, string to)
    {
        var student = new Student { Status = from };

        var result = student.TransitionTo(to, null, Now);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.InvalidState);
        student.Status.Should().Be(from);
    }

    [Fact]
    public void Unknown_status_is_a_validation_error()
    {
        var result = new Student().TransitionTo("graduated", null, Now);

        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Activating_sets_the_start_date_once_and_leaving_sets_the_end_date()
    {
        var student = new Student { Status = StudentStatuses.SignedUp };

        student.TransitionTo(StudentStatuses.Active, null, Now);
        student.StartDate.Should().Be(new DateOnly(2026, 10, 20));

        student.TransitionTo(StudentStatuses.Withdrawn, "moved", Now.AddDays(30));
        student.StartDate.Should().Be(new DateOnly(2026, 10, 20));
        student.EndDate.Should().Be(new DateOnly(2026, 11, 19));
    }
}

public class AppUserLockoutTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Account_locks_after_the_maximum_number_of_failures()
    {
        var user = new AppUser();

        for (var i = 0; i < 4; i++)
        {
            user.RegisterFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        }

        user.IsLockedOut(Now).Should().BeFalse();

        user.RegisterFailedLogin(Now, 5, TimeSpan.FromMinutes(15));

        user.IsLockedOut(Now).Should().BeTrue();
        user.IsLockedOut(Now.AddMinutes(16)).Should().BeFalse();
    }

    [Fact]
    public void Successful_login_clears_failures_and_lockout()
    {
        var user = new AppUser { FailedLoginCount = 3, LockoutEnd = Now.AddMinutes(5) };

        user.RegisterSuccessfulLogin(Now);

        user.FailedLoginCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
        user.LastLoginAt.Should().Be(Now);
    }
}

public class ResultAndPagingTests
{
    [Fact]
    public void Failed_result_refuses_to_expose_a_value()
    {
        var result = Result<int>.Failure(Error.NotFound());

        var read = () => result.Value;

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cursor_round_trips()
    {
        var token = new CursorToken(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), Guid.NewGuid());

        var decoded = CursorToken.TryDecode(token.Encode());

        decoded.Should().Be(token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("bm90LWpzb24=")]
    public void Garbage_cursor_is_ignored(string? cursor) => CursorToken.TryDecode(cursor).Should().BeNull();

    [Theory]
    [InlineData(null, 50)]
    [InlineData(0, 1)]
    [InlineData(1000, 100)]
    [InlineData(25, 25)]
    public void Limit_is_clamped_to_the_allowed_range(int? requested, int expected) =>
        CursorToken.NormalizeLimit(requested).Should().Be(expected);
}
