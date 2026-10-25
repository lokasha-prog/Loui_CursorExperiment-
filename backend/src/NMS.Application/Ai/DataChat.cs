using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Application.Abstractions;
using NMS.Application.Attendance;
using NMS.Application.CentreGroups;
using NMS.Application.Security;
using NMS.Domain.Admissions;
using NMS.Domain.Attendance;
using NMS.Domain.Billing;
using NMS.Domain.CentreManagement;
using NMS.Domain.Learning;
using NMS.Shared.Results;

namespace NMS.Application.Ai;

/// <summary>One data look-up the AI Brain made while answering, shown to the person as "how I worked it out".</summary>
public sealed record DataLookup(string Tool, string Arguments, string Result);

public sealed record DataChatDto(string Answer, string Language, string Model, DateTimeOffset GeneratedAt, IReadOnlyList<DataLookup> Lookups);

/// <summary>A free question about the centre's data, with the conversation so far.</summary>
public sealed record DataChatCommand(string Question, IReadOnlyList<AiMessage>? History, string Language) : IRequest<Result<DataChatDto>>;

public sealed class DataChatValidator : AbstractValidator<DataChatCommand>
{
    public DataChatValidator()
    {
        RuleFor(c => c.Question).NotEmpty().MaximumLength(1000);
        RuleFor(c => c.Language).Must(l => l is "en" or "ar").WithMessage("Language must be en or ar.");
        RuleFor(c => c.History!).Must(h => h.Count <= 12).When(c => c.History is not null).WithMessage("At most 12 earlier messages.");
        RuleForEach(c => c.History!).Must(m => m.Role is "user" or "assistant" && m.Content.Length <= 4000).When(c => c.History is not null)
            .WithMessage("Earlier messages must be from the user or the assistant and at most 4000 characters.");
    }
}

/// <summary>
/// "Chat with data": the model answers free questions by calling the counting tools below, which run fixed, checked queries
/// and return only counts and totals (with room, group and branch names). The model never writes queries and never sees
/// a child's, parent's or staff member's name.
/// </summary>
public sealed class DataChatHandler(
    IAppDbContext db, ITenantContext tenant, ICurrentUser current, CentreCalendar calendar, CentreRunner runner, IClock clock, IAiBrain brain)
    : IRequestHandler<DataChatCommand, Result<DataChatDto>>
{
    private static readonly JsonSerializerOptions Read = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions Write = new(JsonSerializerDefaults.Web);
    private static readonly string[] PresentStatuses = [AttendanceStatuses.Present, AttendanceStatuses.Late, AttendanceStatuses.EarlyLeave];

    public async Task<Result<DataChatDto>> Handle(DataChatCommand request, CancellationToken ct)
    {
        if (!brain.IsConfigured)
        {
            return AiErrors.NotSetUp;
        }

        var lookups = new List<DataLookup>();
        var messages = (request.History ?? []).Append(new AiMessage("user", request.Question)).ToList();
        var answer = await brain.ChatAsync(SystemPrompt(request.Language), messages, Tools(), async (name, args, token) =>
        {
            string result;
            try
            {
                result = await RunAsync(name, string.IsNullOrWhiteSpace(args) ? "{}" : args, token);
            }
            catch (JsonException)
            {
                result = Json(new { error = "The arguments were not valid JSON for this tool." });
            }

            lookups.Add(new DataLookup(name, args, result));
            return result;
        }, ct);

        return answer.IsFailure
            ? Result<DataChatDto>.Failure(answer.Errors)
            : Result<DataChatDto>.Success(new DataChatDto(answer.Value.Trim(), request.Language, brain.ModelName, clock.UtcNow, lookups));
    }

    private Task<string> RunAsync(string name, string args, CancellationToken ct) => name switch
    {
        "centre_info" => CentreInfoAsync(ct),
        "count_children" => CountChildrenAsync(JsonSerializer.Deserialize<ChildArgs>(args, Read) ?? new(), ct),
        "count_staff" => CountStaffAsync(JsonSerializer.Deserialize<StaffArgs>(args, Read) ?? new(), ct),
        "count_leave" => CountLeaveAsync(JsonSerializer.Deserialize<LeaveArgs>(args, Read) ?? new(), ct),
        "money" => MoneyAsync(JsonSerializer.Deserialize<MoneyArgs>(args, Read) ?? new(), ct),
        "count_enquiries" => CountEnquiriesAsync(JsonSerializer.Deserialize<EnquiryArgs>(args, Read) ?? new(), ct),
        "branches" => BranchesAsync(ct),
        "rooms" => RoomStatusAsync(JsonSerializer.Deserialize<RoomArgs>(args, Read) ?? new(), ct),
        "curriculum" => CurriculumAsync(JsonSerializer.Deserialize<LearningArgs>(args, Read) ?? new(), ct),
        "count_lessons" => CountLessonsAsync(JsonSerializer.Deserialize<LearningArgs>(args, Read) ?? new(), ct),
        "count_observations" => CountObservationsAsync(JsonSerializer.Deserialize<LearningArgs>(args, Read) ?? new(), ct),
        "count_assessments" => CountAssessmentsAsync(JsonSerializer.Deserialize<LearningArgs>(args, Read) ?? new(), ct),
        _ => Task.FromResult(Json(new { error = $"There is no tool called '{name}'.", valid = ToolNames })),
    };

    private static readonly string[] ToolNames =
        ["centre_info", "count_children", "count_staff", "count_leave", "money", "count_enquiries", "branches", "rooms", "curriculum", "count_lessons", "count_observations", "count_assessments"];

    // ── Tool arguments ──────────────────────────────────────────────────────

    private sealed record ChildArgs(
        string? Date = null, string? Room = null, string? Group = null, string? Gender = null, string? Attendance = null,
        string? EnrolmentStatus = null, int? MinAgeMonths = null, int? MaxAgeMonths = null, string? GroupBy = null);

    private sealed record StaffArgs(
        string? Date = null, string? Room = null, string? Role = null, string? JobTitle = null, string? Gender = null,
        string? Attendance = null, bool? OnLeave = null, string? Status = null, string? GroupBy = null);

    private sealed record RoomArgs(string? Date = null, string? Room = null);

    /// <summary>Shared by the learning tools; each uses the filters that apply to it.</summary>
    private sealed record LearningArgs(
        string? From = null, string? To = null, string? Framework = null, string? Area = null, string? AgeBand = null, string? Room = null,
        string? Gender = null, string? Type = null, bool? SharedWithParents = null, bool? Templates = null, string? Term = null,
        string? Status = null, string? Template = null, bool? ParentApproved = null, bool? IncludeScores = null, string? GroupBy = null);

    private sealed record LeaveArgs(
        string? Date = null, string? From = null, string? To = null, string? Person = null, string? Status = null,
        string? LeaveType = null, string? Role = null, string? GroupBy = null);

    private sealed record MoneyArgs(string? Measure = null, string? From = null, string? To = null, string? GroupBy = null);

    private sealed record EnquiryArgs(string? From = null, string? To = null, string? Stage = null, string? Source = null, string? GroupBy = null);

    private sealed record Named(Guid Id, Dictionary<string, string> Name);

    // ── The tools ───────────────────────────────────────────────────────────

    private async Task<string> CentreInfoAsync(CancellationToken ct)
    {
        var today = calendar.Today;
        var children = await db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active && s.RoomId != null)
            .GroupBy(s => s.RoomId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key!.Value, g => g.Count, ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.CreatedAt).ToListAsync(ct);
        var groups = await db.StudentGroups.AsNoTracking().ToListAsync(ct);
        var (_, centres) = await GroupMembers.OfAsync(db, tenant.TenantId, ct);
        return Json(new
        {
            today = today.ToString("yyyy-MM-dd"),
            weekday = today.DayOfWeek.ToString(),
            rooms = rooms.Select(r => new { name = r.Name, capacity = r.Capacity, children_per_staff = r.RatioStaffToStudent, active_children = children.GetValueOrDefault(r.Id) }),
            groups = groups.Select(g => new { name = g.Name, room = rooms.FirstOrDefault(r => r.Id == g.RoomId)?.Name.GetValueOrDefault("en") }),
            leave_types = await db.LeaveTypes.AsNoTracking().Select(l => l.Name).ToListAsync(ct),
            staff_roles = await db.Roles.AsNoTracking().Where(r => r.Name != "parent").Select(r => r.Name).ToListAsync(ct),
            job_titles = await db.Staff.AsNoTracking().Where(s => s.Status == "active" && s.JobTitle != null).Select(s => s.JobTitle!).Distinct().ToListAsync(ct),
            genders = new[] { "female", "male" },
            attendance_values = new[] { "present (includes late and left early)", "absent", "late", "early_leave", "not_checked_in" },
            enquiry_stages = EnquiryStages.All,
            enquiry_sources = await db.Enquiries.AsNoTracking().Select(e => e.Source).Distinct().ToListAsync(ct),
            branches = centres.Where(c => c.Status == "active").Select(c => c.Name),
            curriculum_frameworks = await db.CurriculumFrameworks.AsNoTracking().Select(f => new { name = f.Name, code = f.Code }).ToListAsync(ct),
            development_areas = await db.DevelopmentAreas.AsNoTracking().OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(ct),
            age_bands = await db.AgeBands.AsNoTracking().OrderBy(x => x.MinMonths).Select(x => x.Name).ToListAsync(ct),
            assessment_terms = await db.AssessmentRuns.AsNoTracking().Select(r => r.Term).Distinct().ToListAsync(ct),
            assessment_templates = await db.AssessmentTemplates.AsNoTracking().Select(t => t.Name).ToListAsync(ct),
            observation_types = ObservationTypes.All,
            staff_statuses = StaffStatuses.All,
        });
    }

    private async Task<string> CountChildrenAsync(ChildArgs a, CancellationToken ct)
    {
        if (!TryDate(a.Date, out var date))
        {
            return BadDate(a.Date);
        }

        var children = db.Students.AsNoTracking();
        var enrolment = Lower(a.EnrolmentStatus) ?? StudentStatuses.Active;
        if (enrolment != "any")
        {
            if (!StudentStatuses.All.Contains(enrolment))
            {
                return Json(new { error = $"Unknown enrolment status '{a.EnrolmentStatus}'.", valid = StudentStatuses.All.Append("any") });
            }

            children = children.Where(s => s.Status == enrolment);
        }

        var rooms = await RoomsAsync(ct);
        var groups = await GroupsAsync(ct);
        if (a.Room is { Length: > 0 })
        {
            var room = Find(rooms, a.Room);
            if (room is null)
            {
                return Unknown("room", a.Room, rooms);
            }

            children = children.Where(s => s.RoomId == room.Id);
        }

        if (a.Group is { Length: > 0 })
        {
            var group = Find(groups, a.Group);
            if (group is null)
            {
                return Unknown("group", a.Group, groups);
            }

            children = children.Where(s => s.GroupId == group.Id);
        }

        var gender = Gender(a.Gender);
        if (gender is not null)
        {
            children = children.Where(s => s.Gender == gender);
        }

        if (a.MinAgeMonths is { } min)
        {
            var bornBy = date.AddMonths(-min);
            children = children.Where(s => s.DateOfBirth <= bornBy);
        }

        if (a.MaxAgeMonths is { } max)
        {
            var bornAfter = date.AddMonths(-(max + 1));
            children = children.Where(s => s.DateOfBirth > bornAfter);
        }

        var list = await children.Select(s => new { s.Id, s.RoomId, s.GroupId, s.Gender, s.DateOfBirth, s.Nationality }).ToListAsync(ct);
        var attendance = Lower(a.Attendance);
        var groupBy = Lower(a.GroupBy);
        Dictionary<Guid, string> status = [];
        if ((attendance is not null && attendance != "any") || groupBy == "attendance")
        {
            status = await AttendanceOnAsync(PersonTypes.Student, date, ct);
            if (attendance is not null && attendance != "any")
            {
                if (!AttendanceValues.Contains(attendance))
                {
                    return Json(new { error = $"Unknown attendance value '{a.Attendance}'.", valid = AttendanceValues.Append("any") });
                }

                list = list.Where(s => Matches(status.GetValueOrDefault(s.Id), attendance)).ToList();
            }
        }

        var keys = groupBy switch
        {
            null or "" => null,
            "room" => list.Select(s => NameOf(rooms, s.RoomId, "no room")).ToList(),
            "group" => list.Select(s => NameOf(groups, s.GroupId, "no group")).ToList(),
            "gender" => list.Select(s => s.Gender ?? "not recorded").ToList(),
            "attendance" => list.Select(s => AttendanceLabel(status.GetValueOrDefault(s.Id))).ToList(),
            "age_band" => list.Select(s => AgeBand(s.DateOfBirth, date)).ToList(),
            "nationality" => list.Select(s => s.Nationality ?? "not recorded").ToList(),
            _ => null,
        };
        if (groupBy is { Length: > 0 } && keys is null)
        {
            return Json(new { error = $"Cannot group children by '{a.GroupBy}'.", valid = new[] { "room", "group", "gender", "attendance", "age_band", "nationality" } });
        }

        return Json(new
        {
            date = date.ToString("yyyy-MM-dd"),
            filters = new { a.Room, a.Group, gender, attendance, enrolment_status = enrolment, a.MinAgeMonths, a.MaxAgeMonths },
            count = list.Count,
            breakdown = Breakdown(keys),
        });
    }

    private async Task<string> CountStaffAsync(StaffArgs a, CancellationToken ct)
    {
        if (!TryDate(a.Date, out var date))
        {
            return BadDate(a.Date);
        }

        var employment = Lower(a.Status) ?? "active";
        if (employment != "any" && !StaffStatuses.All.Contains(employment))
        {
            return Json(new { error = $"Unknown staff status '{a.Status}'.", valid = StaffStatuses.All.Append("any") });
        }

        var staff = await db.Staff.AsNoTracking().Where(s => employment == "any" || s.Status == employment)
            .Select(s => new { s.Id, s.AppUserId, s.Gender, s.JobTitle, s.Status, s.HireDate }).ToListAsync(ct);
        var roleRows = await db.AppUserRoles.AsNoTracking().Join(db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.AppUserId, r.Name }).ToListAsync(ct);
        var roles = roleRows.GroupBy(r => r.AppUserId).ToDictionary(g => g.Key, g => g.Select(r => r.Name).ToList());
        List<string> RolesOf(Guid? user) => user is { } u ? roles.GetValueOrDefault(u) ?? [] : [];

        var day = (int)date.DayOfWeek;
        var shifts = await db.StaffRoomAssignments.AsNoTracking()
            .Where(x => x.DayOfWeek == day && x.EffectiveFrom <= date && (x.EffectiveTo == null || x.EffectiveTo >= date))
            .Select(x => new { x.StaffId, x.RoomId }).ToListAsync(ct);
        var onLeave = (await db.LeaveRequests.AsNoTracking()
            .Where(l => l.PersonType == PersonTypes.Staff && l.Status == LeaveStatuses.Approved && l.StartDate <= date && l.EndDate >= date)
            .Select(l => l.PersonId).ToListAsync(ct)).ToHashSet();
        var rooms = await RoomsAsync(ct);

        if (a.Room is { Length: > 0 })
        {
            var room = Find(rooms, a.Room);
            if (room is null)
            {
                return Unknown("room", a.Room, rooms);
            }

            var inRoom = shifts.Where(x => x.RoomId == room.Id).Select(x => x.StaffId).ToHashSet();
            staff = staff.Where(s => inRoom.Contains(s.Id)).ToList();
        }

        var role = Lower(a.Role);
        if (role is { Length: > 0 })
        {
            var known = roleRows.Select(r => r.Name).Distinct().ToList();
            if (!known.Contains(role))
            {
                return Json(new { error = $"Unknown role '{a.Role}'.", valid = known.Where(r => r != "parent") });
            }

            staff = staff.Where(s => RolesOf(s.AppUserId).Contains(role)).ToList();
        }

        if (a.JobTitle is { Length: > 0 })
        {
            staff = staff.Where(s => s.JobTitle != null && s.JobTitle.Contains(a.JobTitle, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var gender = Gender(a.Gender);
        if (gender is not null)
        {
            staff = staff.Where(s => s.Gender == gender).ToList();
        }

        if (a.OnLeave is { } leave)
        {
            staff = staff.Where(s => onLeave.Contains(s.Id) == leave).ToList();
        }

        var attendance = Lower(a.Attendance);
        var groupBy = Lower(a.GroupBy);
        Dictionary<Guid, string> status = [];
        if ((attendance is not null && attendance != "any") || groupBy == "attendance")
        {
            status = await AttendanceOnAsync(PersonTypes.Staff, date, ct);
            if (attendance is not null && attendance != "any")
            {
                if (!AttendanceValues.Contains(attendance))
                {
                    return Json(new { error = $"Unknown attendance value '{a.Attendance}'.", valid = AttendanceValues.Append("any") });
                }

                staff = staff.Where(s => Matches(status.GetValueOrDefault(s.Id), attendance)).ToList();
            }
        }

        var keys = groupBy switch
        {
            null or "" => null,
            "role" => staff.SelectMany(s => RolesOf(s.AppUserId).DefaultIfEmpty("no sign-in account")).ToList(),
            "job_title" => staff.Select(s => s.JobTitle ?? "not recorded").ToList(),
            "room" => staff.SelectMany(s => shifts.Where(x => x.StaffId == s.Id).Select(x => NameOf(rooms, x.RoomId, "no room")).DefaultIfEmpty("not rostered that day")).ToList(),
            "gender" => staff.Select(s => s.Gender ?? "not recorded").ToList(),
            "attendance" => staff.Select(s => AttendanceLabel(status.GetValueOrDefault(s.Id))).ToList(),
            "on_leave" => staff.Select(s => onLeave.Contains(s.Id) ? "on leave" : "not on leave").ToList(),
            "status" => staff.Select(s => s.Status).ToList(),
            "hire_year" => staff.Select(s => s.HireDate.Year.ToString(CultureInfo.InvariantCulture)).ToList(),
            _ => null,
        };
        if (groupBy is { Length: > 0 } && keys is null)
        {
            return Json(new { error = $"Cannot group staff by '{a.GroupBy}'.", valid = new[] { "role", "job_title", "room", "gender", "attendance", "on_leave", "status", "hire_year" } });
        }

        return Json(new
        {
            date = date.ToString("yyyy-MM-dd"),
            filters = new { a.Room, role, a.JobTitle, gender, attendance, on_leave = a.OnLeave, status = employment },
            count = staff.Count,
            breakdown = Breakdown(keys),
            note = groupBy is "role" or "room" ? "One person can count under several roles or rooms." : null,
        });
    }

    private async Task<string> CountLeaveAsync(LeaveArgs a, CancellationToken ct)
    {
        if (!TryRange(a.Date, a.From, a.To, calendar.Today, calendar.Today, out var from, out var to, out var bad))
        {
            return bad;
        }

        var requests = db.LeaveRequests.AsNoTracking().Where(l => l.StartDate <= to && l.EndDate >= from);
        var person = Lower(a.Person) ?? "all";
        requests = person switch
        {
            "staff" => requests.Where(l => l.PersonType == PersonTypes.Staff),
            "children" or "child" or "students" => requests.Where(l => l.PersonType == PersonTypes.Student),
            _ => requests,
        };
        var status = Lower(a.Status) ?? LeaveStatuses.Approved;
        if (status != "any")
        {
            requests = requests.Where(l => l.Status == status);
        }

        var types = await db.LeaveTypes.AsNoTracking().Select(t => new Named(t.Id, t.Name)).ToListAsync(ct);
        if (a.LeaveType is { Length: > 0 })
        {
            var type = Find(types, a.LeaveType);
            if (type is null)
            {
                return Unknown("leave type", a.LeaveType, types);
            }

            requests = requests.Where(l => l.LeaveTypeId == type.Id);
        }

        var rows = await requests.Select(l => new { l.PersonType, l.PersonId, l.LeaveTypeId, l.Status }).ToListAsync(ct);
        var staffRoles = await db.Staff.AsNoTracking().Where(s => s.AppUserId != null)
            .Join(db.AppUserRoles.AsNoTracking(), s => s.AppUserId, ur => ur.AppUserId, (s, ur) => new { s.Id, ur.RoleId })
            .Join(db.Roles.AsNoTracking(), x => x.RoleId, r => r.Id, (x, r) => new { x.Id, r.Name }).ToListAsync(ct);
        if (Lower(a.Role) is { Length: > 0 } role)
        {
            var withRole = staffRoles.Where(r => r.Name == role).Select(r => r.Id).ToHashSet();
            rows = rows.Where(r => r.PersonType == PersonTypes.Staff && withRole.Contains(r.PersonId)).ToList();
        }

        var keys = Lower(a.GroupBy) switch
        {
            null or "" => null,
            "person" => rows.Select(r => r.PersonType == PersonTypes.Staff ? "staff" : "children").ToList(),
            "leave_type" => rows.Select(r => NameOf(types, r.LeaveTypeId, "unknown")).ToList(),
            "status" => rows.Select(r => r.Status).ToList(),
            "role" => rows.SelectMany(r => staffRoles.Where(s => s.Id == r.PersonId).Select(s => s.Name).DefaultIfEmpty(r.PersonType == PersonTypes.Staff ? "no sign-in account" : "child")).ToList(),
            _ => null,
        };
        return Json(new
        {
            from = from.ToString("yyyy-MM-dd"),
            to = to.ToString("yyyy-MM-dd"),
            filters = new { person, status, a.LeaveType, a.Role },
            requests = rows.Count,
            people = rows.Select(r => r.PersonId).Distinct().Count(),
            breakdown = Breakdown(keys),
        });
    }

    private async Task<string> MoneyAsync(MoneyArgs a, CancellationToken ct)
    {
        var today = calendar.Today;
        if (!TryRange(null, a.From, a.To, new DateOnly(today.Year, today.Month, 1), today, out var from, out var to, out var bad))
        {
            return bad;
        }

        var measure = Lower(a.Measure) ?? "billed";
        var groupBy = Lower(a.GroupBy);
        List<(DateOnly Date, string Key, long Fils)> rows;
        switch (measure)
        {
            case "billed":
                rows = (await db.Invoices.AsNoTracking()
                    .Where(i => i.IssueDate >= from && i.IssueDate <= to && i.Status != InvoiceStatuses.Draft && i.Status != InvoiceStatuses.Cancelled)
                    .Select(i => new { i.IssueDate, i.Status, i.TotalFils }).ToListAsync(ct)).Select(i => (i.IssueDate, i.Status, i.TotalFils)).ToList();
                break;
            case "collected":
                rows = (await db.InvoicePayments.AsNoTracking().Where(p => p.PaymentDate >= from && p.PaymentDate <= to)
                    .Select(p => new { p.PaymentDate, p.AmountFils }).ToListAsync(ct)).Select(p => (p.PaymentDate, "payment", p.AmountFils)).ToList();
                break;
            case "outstanding" or "overdue":
                var open = await db.Invoices.AsNoTracking().Where(i => InvoiceStatuses.Open.Contains(i.Status))
                    .Select(i => new { i.DueDate, i.Status, Left = i.TotalFils - i.PaidFils }).ToListAsync(ct);
                rows = open.Where(i => measure == "outstanding" || i.DueDate < today).Select(i => (i.DueDate, i.Status, i.Left)).ToList();
                (from, to) = (rows.Count > 0 ? rows.Min(r => r.Date) : today, today);
                break;
            case "expenses":
                rows = (await db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
                    .Select(e => new { e.ExpenseDate, e.Category, e.AmountFils }).ToListAsync(ct)).Select(e => (e.ExpenseDate, e.Category, e.AmountFils)).ToList();
                break;
            default:
                return Json(new { error = $"Unknown measure '{a.Measure}'.", valid = new[] { "billed", "collected", "outstanding", "overdue", "expenses" } });
        }

        object? breakdown = groupBy switch
        {
            "month" => rows.GroupBy(r => r.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture)).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => Aed(g.Sum(r => r.Fils))),
            "category" or "status" => rows.GroupBy(r => r.Key).OrderByDescending(g => g.Sum(r => r.Fils)).ToDictionary(g => g.Key, g => Aed(g.Sum(r => r.Fils))),
            _ => null,
        };
        return Json(new
        {
            measure,
            from = from.ToString("yyyy-MM-dd"),
            to = to.ToString("yyyy-MM-dd"),
            total = Aed(rows.Sum(r => r.Fils)),
            items = rows.Count,
            breakdown,
            note = measure is "outstanding" or "overdue" ? "Money still owed on open invoices as of today, whatever the date range." : null,
        });
    }

    private async Task<string> CountEnquiriesAsync(EnquiryArgs a, CancellationToken ct)
    {
        var today = calendar.Today;
        if (!TryRange(null, a.From, a.To, today.AddDays(-89), today, out var from, out var to, out var bad))
        {
            return bad;
        }

        var enquiries = db.Enquiries.AsNoTracking().Where(e => e.EnquiryDate >= from && e.EnquiryDate <= to);
        if (Lower(a.Stage) is { Length: > 0 } stage)
        {
            if (!EnquiryStages.All.Contains(stage))
            {
                return Json(new { error = $"Unknown stage '{a.Stage}'.", valid = EnquiryStages.All });
            }

            enquiries = enquiries.Where(e => e.Stage == stage);
        }

        if (Lower(a.Source) is { Length: > 0 } source)
        {
            enquiries = enquiries.Where(e => e.Source == source);
        }

        var rows = await enquiries.Select(e => new { e.Stage, e.Source, e.EnquiryDate }).ToListAsync(ct);
        var keys = Lower(a.GroupBy) switch
        {
            "stage" => rows.Select(r => r.Stage).ToList(),
            "source" => rows.Select(r => r.Source).ToList(),
            "month" => rows.Select(r => r.EnquiryDate.ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList(),
            _ => null,
        };
        return Json(new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"), filters = new { a.Stage, a.Source }, count = rows.Count, breakdown = Breakdown(keys) });
    }

    private async Task<string> BranchesAsync(CancellationToken ct)
    {
        if (!current.HasPermission(Permissions.GroupRead))
        {
            return Json(new { error = "Figures for other branches need permission to see the group of centres." });
        }

        var rows = await GroupFigures.ForGroupAsync(db, tenant, runner, ct);
        return Json(new
        {
            month = calendar.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            branches = rows.Select(r => new
            {
                name = r.Name,
                head_centre = r.IsHead,
                active_children = r.Students,
                active_staff = r.Staff,
                children_checked_in_today = r.PresentToday,
                joined_this_month = r.JoinedThisMonth,
                left_this_month = r.LeftThisMonth,
                billed_this_month = Aed(r.BilledFils),
                collected_this_month = Aed(r.CollectedFils),
                overdue = Aed(r.OverdueFils),
                spent_this_month = Aed(r.ExpensesFils),
                open_enquiries = r.OpenEnquiries,
            }),
        });
    }

    private async Task<string> RoomStatusAsync(RoomArgs a, CancellationToken ct)
    {
        if (!TryDate(a.Date, out var date))
        {
            return BadDate(a.Date);
        }

        var rooms = await db.Rooms.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.CreatedAt).ToListAsync(ct);
        if (a.Room is { Length: > 0 })
        {
            var room = Find(rooms.Select(r => new Named(r.Id, r.Name)).ToList(), a.Room);
            if (room is null)
            {
                return Unknown("room", a.Room, rooms.Select(r => new Named(r.Id, r.Name)).ToList());
            }

            rooms = rooms.Where(r => r.Id == room.Id).ToList();
        }

        var children = await db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active && s.RoomId != null)
            .Select(s => new { s.Id, RoomId = s.RoomId!.Value }).ToListAsync(ct);
        var childRecords = await db.AttendanceRecords.AsNoTracking().Where(r => r.PersonType == PersonTypes.Student && r.Date == date)
            .Select(r => new { r.PersonId, r.Status, r.CheckInAt, r.CheckOutAt }).ToListAsync(ct);
        var childStatus = childRecords.GroupBy(r => r.PersonId).ToDictionary(g => g.Key, g => g.First());
        var day = (int)date.DayOfWeek;
        var shifts = await db.StaffRoomAssignments.AsNoTracking()
            .Where(x => x.DayOfWeek == day && x.EffectiveFrom <= date && (x.EffectiveTo == null || x.EffectiveTo >= date))
            .Join(db.Staff.AsNoTracking().Where(s => s.Status == "active"), x => x.StaffId, s => s.Id, (x, s) => new { x.StaffId, x.RoomId }).ToListAsync(ct);
        var staffStatus = await AttendanceOnAsync(PersonTypes.Staff, date, ct);
        var onLeave = (await db.LeaveRequests.AsNoTracking()
            .Where(l => l.PersonType == PersonTypes.Staff && l.Status == LeaveStatuses.Approved && l.StartDate <= date && l.EndDate >= date)
            .Select(l => l.PersonId).ToListAsync(ct)).ToHashSet();
        var groups = await db.StudentGroups.AsNoTracking().GroupBy(g => g.RoomId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        return Json(new
        {
            date = date.ToString("yyyy-MM-dd"),
            rooms = rooms.Select(r =>
            {
                var kids = children.Where(c => c.RoomId == r.Id).Select(c => c.Id).ToList();
                var present = kids.Count(k => Matches(childStatus.GetValueOrDefault(k)?.Status, "present"));
                var inNow = kids.Count(k => childStatus.GetValueOrDefault(k) is { CheckInAt: not null, CheckOutAt: null } s && PresentStatuses.Contains(s.Status));
                var rostered = shifts.Where(x => x.RoomId == r.Id).Select(x => x.StaffId).Distinct().ToList();
                var staffPresent = rostered.Count(s => Matches(staffStatus.GetValueOrDefault(s), "present"));
                var needed = r.RatioStaffToStudent > 0 ? (int)Math.Ceiling(present / (double)r.RatioStaffToStudent) : 0;
                return new
                {
                    name = r.Name.GetValueOrDefault("en") ?? r.Name.Values.FirstOrDefault(),
                    capacity = r.Capacity,
                    age_range_months = $"{r.MinAgeMonths}-{r.MaxAgeMonths}",
                    children_per_staff = r.RatioStaffToStudent,
                    groups = groups.GetValueOrDefault(r.Id),
                    active_children = kids.Count,
                    free_places = Math.Max(0, r.Capacity - kids.Count),
                    occupancy_percent = r.Capacity > 0 ? Math.Round(kids.Count * 100.0 / r.Capacity) : 0,
                    children_present = present,
                    children_absent = kids.Count(k => childStatus.GetValueOrDefault(k)?.Status == AttendanceStatuses.Absent),
                    children_in_the_room_now = inNow,
                    staff_rostered = rostered.Count,
                    staff_rostered_on_leave = rostered.Count(onLeave.Contains),
                    staff_rostered_present = staffPresent,
                    staff_needed_for_ratio = needed,
                    ratio_met = staffPresent >= needed,
                };
            }),
            note = "Children count in the room they are placed in. Staff count in the room they are rostered in on that weekday; one person can be rostered in several rooms.",
        });
    }

    /// <summary>The framework, area, age band and room named in a learning question, checked against the centre's own lists.</summary>
    private sealed record LearningScope(Guid? Framework, Guid? Area, Guid? Band, Guid? Room, List<Named> Frameworks, List<Named> Areas, List<Named> Bands, List<Named> Rooms);

    private async Task<(LearningScope? Scope, string Error)> ScopeAsync(LearningArgs a, CancellationToken ct)
    {
        var frameworks = (await db.CurriculumFrameworks.AsNoTracking().Select(f => new { f.Id, f.Code, f.Name }).ToListAsync(ct))
            .Select(f => new Named(f.Id, new Dictionary<string, string>(f.Name) { ["code"] = f.Code })).ToList();
        Guid? framework = null;
        if (a.Framework is { Length: > 0 })
        {
            if (Find(frameworks, a.Framework) is not { } found)
            {
                return (null, Unknown("curriculum framework", a.Framework, frameworks));
            }

            framework = found.Id;
        }

        var areas = await db.DevelopmentAreas.AsNoTracking().Where(x => framework == null || x.FrameworkId == framework).OrderBy(x => x.SortOrder)
            .Select(x => new Named(x.Id, x.Name)).ToListAsync(ct);
        var bands = await db.AgeBands.AsNoTracking().Where(x => framework == null || x.FrameworkId == framework).OrderBy(x => x.MinMonths)
            .Select(x => new Named(x.Id, x.Name)).ToListAsync(ct);
        var rooms = await RoomsAsync(ct);
        Guid? area = null, band = null, room = null;
        if (a.Area is { Length: > 0 })
        {
            if (Find(areas, a.Area) is not { } found)
            {
                return (null, Unknown("development area", a.Area, areas));
            }

            area = found.Id;
        }

        if (a.AgeBand is { Length: > 0 })
        {
            if (Find(bands, a.AgeBand) is not { } found)
            {
                return (null, Unknown("age band", a.AgeBand, bands));
            }

            band = found.Id;
        }

        if (a.Room is { Length: > 0 })
        {
            if (Find(rooms, a.Room) is not { } found)
            {
                return (null, Unknown("room", a.Room, rooms));
            }

            room = found.Id;
        }

        return (new LearningScope(framework, area, band, room, frameworks, areas, bands, rooms), string.Empty);
    }

    private async Task<string> CurriculumAsync(LearningArgs a, CancellationToken ct)
    {
        var (scope, error) = await ScopeAsync(a, ct);
        if (scope is null)
        {
            return error;
        }

        var milestones = await db.Milestones.AsNoTracking().Where(m => scope.Framework == null || m.FrameworkId == scope.Framework)
            .Select(m => new { m.Id, m.FrameworkId, m.AreaId, m.AgeBandId }).ToListAsync(ct);
        var areas = await db.DevelopmentAreas.AsNoTracking().Select(x => new { x.Id, x.FrameworkId }).ToListAsync(ct);
        var bands = await db.AgeBands.AsNoTracking().Select(x => new { x.Id, x.FrameworkId, x.MinMonths, x.MaxMonths }).ToListAsync(ct);
        var lessons = await db.Lessons.AsNoTracking().Select(l => new { l.FrameworkId, l.IsTemplate, l.MilestoneIds }).ToListAsync(ct);
        var templates = await db.AssessmentTemplates.AsNoTracking().Select(t => new { t.FrameworkId }).ToListAsync(ct);
        var observed = (await db.Observations.AsNoTracking().Select(o => o.MilestoneIds).ToListAsync(ct))
            .SelectMany(ids => ids).ToHashSet();
        var planned = lessons.SelectMany(l => l.MilestoneIds).ToHashSet();

        return Json(new
        {
            frameworks = scope.Frameworks.Where(f => scope.Framework == null || f.Id == scope.Framework).Select(f =>
            {
                var own = milestones.Where(m => m.FrameworkId == f.Id).ToList();
                return new
                {
                    name = f.Name.GetValueOrDefault("en") ?? f.Name.Values.FirstOrDefault(),
                    code = f.Name["code"],
                    areas = areas.Count(x => x.FrameworkId == f.Id),
                    age_bands = bands.Where(x => x.FrameworkId == f.Id).Select(x => $"{x.MinMonths}-{x.MaxMonths} months"),
                    milestones = own.Count,
                    milestones_planned_in_lessons = own.Count(m => planned.Contains(m.Id.ToString())),
                    milestones_seen_in_observations = own.Count(m => observed.Contains(m.Id.ToString())),
                    lessons = lessons.Count(l => l.FrameworkId == f.Id && !l.IsTemplate),
                    lesson_templates = lessons.Count(l => l.FrameworkId == f.Id && l.IsTemplate),
                    assessment_templates = templates.Count(t => t.FrameworkId == f.Id),
                };
            }),
            milestones_by_area = scope.Framework is null ? null : Breakdown(milestones.Select(m => NameOf(scope.Areas, m.AreaId, "no area")).ToList()),
            milestones_by_age_band = scope.Framework is null ? null : Breakdown(milestones.Select(m => NameOf(scope.Bands, m.AgeBandId, "no age band")).ToList()),
        });
    }

    private async Task<string> CountLessonsAsync(LearningArgs a, CancellationToken ct)
    {
        var (scope, error) = await ScopeAsync(a, ct);
        if (scope is null)
        {
            return error;
        }

        var lessons = db.Lessons.AsNoTracking();
        var dated = a.From is { Length: > 0 } || a.To is { Length: > 0 };
        if (dated)
        {
            if (!TryRange(null, a.From, a.To, new DateOnly(2000, 1, 1), calendar.Today, out var from, out var to, out var bad))
            {
                return bad;
            }

            var (start, end) = (StartOf(from), StartOf(to.AddDays(1)));
            lessons = lessons.Where(l => l.CreatedAt >= start && l.CreatedAt < end);
        }

        if (scope.Framework is { } fw)
        {
            lessons = lessons.Where(l => l.FrameworkId == fw);
        }

        if (scope.Area is { } area)
        {
            lessons = lessons.Where(l => l.AreaId == area);
        }

        if (scope.Band is { } band)
        {
            lessons = lessons.Where(l => l.AgeBandId == band);
        }

        if (scope.Room is { } room)
        {
            lessons = lessons.Where(l => l.RoomId == room);
        }

        if (a.Templates is { } templates)
        {
            lessons = lessons.Where(l => l.IsTemplate == templates);
        }

        var rows = await lessons.Select(l => new { l.FrameworkId, l.AreaId, l.AgeBandId, l.RoomId, l.IsTemplate, l.DurationMinutes, l.CreatedAt }).ToListAsync(ct);
        var keys = Lower(a.GroupBy) switch
        {
            null or "" => null,
            "framework" => rows.Select(r => NameOf(scope.Frameworks, r.FrameworkId, "unknown")).ToList(),
            "area" => rows.Select(r => NameOf(scope.Areas, r.AreaId, "no area")).ToList(),
            "age_band" => rows.Select(r => NameOf(scope.Bands, r.AgeBandId, "no age band")).ToList(),
            "room" => rows.Select(r => NameOf(scope.Rooms, r.RoomId, "whole centre")).ToList(),
            "template" => rows.Select(r => r.IsTemplate ? "template" : "lesson").ToList(),
            "month" => rows.Select(r => LocalDay(r.CreatedAt).ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList(),
            _ => [],
        };
        if (keys is { Count: 0 } && Lower(a.GroupBy) is { } g && !LessonGroups.Contains(g))
        {
            return Json(new { error = $"Cannot group lessons by '{a.GroupBy}'.", valid = LessonGroups });
        }

        return Json(new
        {
            filters = new { a.Framework, a.Area, a.AgeBand, a.Room, templates = a.Templates, created_from = dated ? a.From : null, created_to = dated ? a.To : null },
            count = rows.Count,
            total_minutes = rows.Sum(r => r.DurationMinutes),
            breakdown = Breakdown(keys),
            note = "Lessons are not planned on dates, so a date range means when the lesson was written. A lesson with no room is for the whole centre.",
        });
    }

    private static readonly string[] LessonGroups = ["framework", "area", "age_band", "room", "template", "month"];

    private async Task<string> CountObservationsAsync(LearningArgs a, CancellationToken ct)
    {
        var today = calendar.Today;
        if (!TryRange(null, a.From, a.To, new DateOnly(today.Year, today.Month, 1), today, out var from, out var to, out var bad))
        {
            return bad;
        }

        var (scope, error) = await ScopeAsync(a, ct);
        if (scope is null)
        {
            return error;
        }

        var (start, end) = (StartOf(from), StartOf(to.AddDays(1)));
        var observations = db.Observations.AsNoTracking().Where(o => o.ObservedAt >= start && o.ObservedAt < end);
        var type = Lower(a.Type);
        if (type is not null)
        {
            if (!ObservationTypes.All.Contains(type))
            {
                return Json(new { error = $"Unknown observation type '{a.Type}'.", valid = ObservationTypes.All });
            }

            observations = observations.Where(o => o.Type == type);
        }

        if (scope.Framework is { } fw)
        {
            observations = observations.Where(o => o.FrameworkId == fw);
        }

        if (scope.Area is { } area)
        {
            observations = observations.Where(o => o.AreaId == area);
        }

        if (scope.Band is { } band)
        {
            observations = observations.Where(o => o.AgeBandId == band);
        }

        if (a.SharedWithParents is { } shared)
        {
            observations = observations.Where(o => o.ShareWithParents == shared);
        }

        var rows = await observations.Join(db.Students.AsNoTracking(), o => o.StudentId, s => s.Id,
                (o, s) => new { o.StudentId, o.Type, o.FrameworkId, o.AreaId, o.AgeBandId, o.ShareWithParents, o.ObservedAt, s.RoomId, s.Gender })
            .ToListAsync(ct);
        if (scope.Room is { } room)
        {
            rows = rows.Where(r => r.RoomId == room).ToList();
        }

        var gender = Gender(a.Gender);
        if (gender is not null)
        {
            rows = rows.Where(r => r.Gender == gender).ToList();
        }

        var keys = Lower(a.GroupBy) switch
        {
            null or "" => null,
            "type" => rows.Select(r => r.Type).ToList(),
            "framework" => rows.Select(r => NameOf(scope.Frameworks, r.FrameworkId, "no framework")).ToList(),
            "area" => rows.Select(r => NameOf(scope.Areas, r.AreaId, "no area")).ToList(),
            "age_band" => rows.Select(r => NameOf(scope.Bands, r.AgeBandId, "no age band")).ToList(),
            "room" => rows.Select(r => NameOf(scope.Rooms, r.RoomId, "no room")).ToList(),
            "gender" => rows.Select(r => r.Gender ?? "not recorded").ToList(),
            "shared" => rows.Select(r => r.ShareWithParents ? "shared with parents" : "staff only").ToList(),
            "month" => rows.Select(r => LocalDay(r.ObservedAt).ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList(),
            _ => [],
        };
        if (keys is { Count: 0 } && Lower(a.GroupBy) is { } g && !ObservationGroups.Contains(g))
        {
            return Json(new { error = $"Cannot group observations by '{a.GroupBy}'.", valid = ObservationGroups });
        }

        // Children in the same room (and gender) who have no observation of this kind in the range.
        var active = db.Students.AsNoTracking().Where(s => s.Status == StudentStatuses.Active);
        if (scope.Room is { } inRoom)
        {
            active = active.Where(s => s.RoomId == inRoom);
        }

        if (gender is not null)
        {
            active = active.Where(s => s.Gender == gender);
        }

        var observedChildren = rows.Select(r => r.StudentId).ToHashSet();
        var activeIds = await active.Select(s => s.Id).ToListAsync(ct);
        return Json(new
        {
            from = from.ToString("yyyy-MM-dd"),
            to = to.ToString("yyyy-MM-dd"),
            filters = new { type, a.Framework, a.Area, a.AgeBand, a.Room, gender, shared_with_parents = a.SharedWithParents },
            count = rows.Count,
            children_observed = observedChildren.Count,
            active_children_with_none = activeIds.Count(id => !observedChildren.Contains(id)),
            breakdown = Breakdown(keys),
            note = "Room means the room the child is placed in now.",
        });
    }

    private static readonly string[] ObservationGroups = ["type", "framework", "area", "age_band", "room", "gender", "shared", "month"];

    private async Task<string> CountAssessmentsAsync(LearningArgs a, CancellationToken ct)
    {
        var (scope, error) = await ScopeAsync(a, ct);
        if (scope is null)
        {
            return error;
        }

        var runs = db.AssessmentRuns.AsNoTracking().Join(db.AssessmentTemplates.AsNoTracking(), r => r.TemplateId, t => t.Id,
            (r, t) => new { r.Id, r.StudentId, r.TemplateId, r.Term, r.AssessedOn, r.Status, r.ParentApproved, t.FrameworkId, t.AgeBandId, t.ScoringKind, t.Name });
        var dated = a.From is { Length: > 0 } || a.To is { Length: > 0 };
        if (dated)
        {
            if (!TryRange(null, a.From, a.To, new DateOnly(2000, 1, 1), calendar.Today, out var from, out var to, out var bad))
            {
                return bad;
            }

            runs = runs.Where(r => r.AssessedOn >= from && r.AssessedOn <= to);
        }

        if (scope.Framework is { } fw)
        {
            runs = runs.Where(r => r.FrameworkId == fw);
        }

        if (scope.Band is { } band)
        {
            runs = runs.Where(r => r.AgeBandId == band);
        }

        if (a.Term is { Length: > 0 })
        {
            var term = a.Term.Trim();
            runs = runs.Where(r => r.Term == term);
        }

        var status = Lower(a.Status);
        if (status is not null)
        {
            if (!AssessmentStatuses.All.Contains(status))
            {
                return Json(new { error = $"Unknown assessment status '{a.Status}'.", valid = AssessmentStatuses.All });
            }

            runs = runs.Where(r => r.Status == status);
        }

        if (a.ParentApproved is { } approved)
        {
            runs = runs.Where(r => r.ParentApproved == approved);
        }

        var rows = await runs.Join(db.Students.AsNoTracking(), r => r.StudentId, s => s.Id, (r, s) => new { Run = r, s.RoomId, s.Gender }).ToListAsync(ct);
        var templateNames = rows.Select(r => new Named(r.Run.TemplateId, r.Run.Name)).DistinctBy(t => t.Id).ToList();
        if (a.Template is { Length: > 0 })
        {
            var all = await db.AssessmentTemplates.AsNoTracking().Select(t => new Named(t.Id, t.Name)).ToListAsync(ct);
            if (Find(all, a.Template) is not { } template)
            {
                return Unknown("assessment template", a.Template, all);
            }

            rows = rows.Where(r => r.Run.TemplateId == template.Id).ToList();
        }

        if (scope.Room is { } room)
        {
            rows = rows.Where(r => r.RoomId == room).ToList();
        }

        var gender = Gender(a.Gender);
        if (gender is not null)
        {
            rows = rows.Where(r => r.Gender == gender).ToList();
        }

        var groupBy = Lower(a.GroupBy);
        var keys = groupBy switch
        {
            null or "" => null,
            "status" => rows.Select(r => r.Run.Status).ToList(),
            "term" => rows.Select(r => r.Run.Term).ToList(),
            "template" => rows.Select(r => NameOf(templateNames, r.Run.TemplateId, "unknown")).ToList(),
            "room" => rows.Select(r => NameOf(scope.Rooms, r.RoomId, "no room")).ToList(),
            "gender" => rows.Select(r => r.Gender ?? "not recorded").ToList(),
            "parent_approved" => rows.Select(r => r.Run.ParentApproved ? "approved for parents" : "not approved for parents").ToList(),
            "month" => rows.Select(r => r.Run.AssessedOn.ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList(),
            _ => [],
        };
        if (keys is { Count: 0 } && groupBy is { } g && !AssessmentGroups.Contains(g))
        {
            return Json(new { error = $"Cannot group assessments by '{a.GroupBy}'.", valid = AssessmentGroups });
        }

        // Milestone scores by area: three-point 2 secure, 1 emerging, 0 not yet; yes/no 1 achieved, 0 not yet.
        object? scores = null;
        if (a.IncludeScores == true || scope.Area is not null)
        {
            var runIds = rows.Select(r => r.Run.Id).ToHashSet();
            var kinds = rows.ToDictionary(r => r.Run.Id, r => r.Run.ScoringKind);
            var scored = (await db.AssessmentScores.AsNoTracking().Join(db.Milestones.AsNoTracking(), s => s.MilestoneId, m => m.Id, (s, m) => new { s.RunId, s.Score, m.AreaId })
                .ToListAsync(ct)).Where(s => runIds.Contains(s.RunId) && (scope.Area is null || s.AreaId == scope.Area)).ToList();
            var areaNames = await db.DevelopmentAreas.AsNoTracking().Select(x => new Named(x.Id, x.Name)).ToListAsync(ct);
            scores = scored.GroupBy(s => NameOf(areaNames, s.AreaId, "no area")).ToDictionary(
                g => g.Key,
                g => g.GroupBy(s => ScoreLabel(kinds[s.RunId], s.Score)).ToDictionary(x => x.Key, x => x.Count()));
        }

        return Json(new
        {
            filters = new { a.Framework, a.AgeBand, a.Area, a.Room, a.Term, status, a.Template, gender, parent_approved = a.ParentApproved, assessed_from = dated ? a.From : null, assessed_to = dated ? a.To : null },
            count = rows.Count,
            children_assessed = rows.Select(r => r.Run.StudentId).Distinct().Count(),
            breakdown = Breakdown(keys),
            milestone_scores_by_area = scores,
            note = "Room means the room the child is placed in now.",
        });
    }

    private static readonly string[] AssessmentGroups = ["status", "term", "template", "room", "gender", "parent_approved", "month"];

    private static string ScoreLabel(string kind, int score) => kind == ScoringKinds.Binary
        ? score >= 1 ? "achieved" : "not yet"
        : score switch { >= 2 => "secure", 1 => "emerging", _ => "not yet" };

    private DateTimeOffset StartOf(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, calendar.Zone.GetUtcOffset(local));
    }

    private DateOnly LocalDay(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, calendar.Zone).DateTime);

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static readonly string[] AttendanceValues = ["present", "absent", "late", "early_leave", "not_checked_in"];

    private static bool Matches(string? status, string wanted) => wanted switch
    {
        "present" => status is not null && PresentStatuses.Contains(status),
        "not_checked_in" => status is null || status == AttendanceStatuses.Pending,
        _ => status == wanted,
    };

    private static string AttendanceLabel(string? status) => status is null || status == AttendanceStatuses.Pending ? "not_checked_in" : status;

    private async Task<Dictionary<Guid, string>> AttendanceOnAsync(string personType, DateOnly date, CancellationToken ct) =>
        (await db.AttendanceRecords.AsNoTracking().Where(r => r.PersonType == personType && r.Date == date)
            .Select(r => new { r.PersonId, r.Status }).ToListAsync(ct))
        .GroupBy(r => r.PersonId).ToDictionary(g => g.Key, g => g.First().Status);

    private async Task<List<Named>> RoomsAsync(CancellationToken ct) =>
        await db.Rooms.AsNoTracking().Select(r => new Named(r.Id, r.Name)).ToListAsync(ct);

    private async Task<List<Named>> GroupsAsync(CancellationToken ct) =>
        await db.StudentGroups.AsNoTracking().Select(g => new Named(g.Id, g.Name)).ToListAsync(ct);

    /// <summary>Exact name in any language first, then a name that contains the words given.</summary>
    private static Named? Find(List<Named> items, string name)
    {
        var wanted = name.Trim();
        return items.FirstOrDefault(i => i.Name.Values.Any(v => string.Equals(v.Trim(), wanted, StringComparison.OrdinalIgnoreCase)))
            ?? items.FirstOrDefault(i => i.Name.Values.Any(v => v.Contains(wanted, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NameOf(List<Named> items, Guid? id, string missing) =>
        id is { } v && items.FirstOrDefault(i => i.Id == v) is { } found ? found.Name.GetValueOrDefault("en") ?? found.Name.Values.FirstOrDefault() ?? missing : missing;

    private static string Unknown(string what, string given, List<Named> items) =>
        Json(new { error = $"There is no {what} called '{given}'.", valid = items.Select(i => i.Name.GetValueOrDefault("en") ?? i.Name.Values.FirstOrDefault()) });

    private static Dictionary<string, int>? Breakdown(List<string>? keys) =>
        keys?.GroupBy(k => k).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count());

    private static string? Lower(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant().Replace(' ', '_');

    private static string? Gender(string? value) => Lower(value) switch
    {
        "female" or "girl" or "girls" or "f" or "woman" or "women" => "female",
        "male" or "boy" or "boys" or "m" or "man" or "men" => "male",
        _ => null,
    };

    private static string AgeBand(DateOnly born, DateOnly on)
    {
        var months = ((on.Year - born.Year) * 12) + on.Month - born.Month - (on.Day < born.Day ? 1 : 0);
        return months switch { < 12 => "under 1 year", < 24 => "1 year", < 36 => "2 years", < 48 => "3 years", _ => "4 years or more" };
    }

    private bool TryDate(string? value, out DateOnly date)
    {
        var today = calendar.Today;
        switch (Lower(value))
        {
            case null or "today":
                date = today;
                return true;
            case "yesterday":
                date = today.AddDays(-1);
                return true;
            case "tomorrow":
                date = today.AddDays(1);
                return true;
            default:
                return DateOnly.TryParseExact(value!.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }

    private bool TryRange(string? date, string? fromText, string? toText, DateOnly defaultFrom, DateOnly defaultTo, out DateOnly from, out DateOnly to, out string bad)
    {
        bad = string.Empty;
        from = defaultFrom;
        to = defaultTo;
        if (date is { Length: > 0 })
        {
            if (!TryDate(date, out from))
            {
                bad = BadDate(date);
                return false;
            }

            to = from;
            return true;
        }

        if (fromText is { Length: > 0 } && !TryDate(fromText, out from))
        {
            bad = BadDate(fromText);
            return false;
        }

        if (toText is { Length: > 0 } && !TryDate(toText, out to))
        {
            bad = BadDate(toText);
            return false;
        }

        if (to < from)
        {
            (from, to) = (to, from);
        }

        return true;
    }

    private static string BadDate(string? value) => Json(new { error = $"'{value}' is not a date. Use today, yesterday, tomorrow or yyyy-mm-dd." });

    private static string Aed(long fils) => (fils / 100m).ToString("N2", CultureInfo.InvariantCulture) + " AED";

    private static string Json(object value) => JsonSerializer.Serialize(value, Write);

    // ── What the model is told ──────────────────────────────────────────────

    private static string SystemPrompt(string language) =>
        "You are the data assistant inside a nursery management system in the UAE, answering the nursery's owner. "
        + "Always use the tools to get numbers: never guess, never estimate, never invent a figure, and answer only from what the tools return. "
        + "The tools cover children, attendance, staff, leave, rooms and ratios, money, enquiries, branches, and learning: the curriculum and milestones, lessons, observations (including concerns) and assessments. "
        + "Use centre_info first when you need the exact names of rooms, groups, roles, leave types, frameworks, areas, age bands or terms, or today's date. "
        + "The tools return only counts and totals; you never see names of children, parents or staff and must not ask for them. "
        + "If a question cannot be answered with the tools (for example it needs a person's name or a detail that is not recorded), say so plainly and suggest the screen or report to use. "
        + "Words like teacher, nurse or assistant are usually job titles, so try job_title when the role is not known. "
        + "Answer with one full sentence that starts with the number and says what was counted and when, for example "
        + "'2 girls are present in Busy Bees today (2026-10-25).', then one line starting with a dash for each item of a breakdown. "
        + "Copy dates exactly as the tools return them, never from memory. Keep it short. "
        + "Plain text only: no markdown, no asterisks, no tables, no headings; lines starting with a dash are fine. Answer in "
        + (language == "ar" ? "Arabic (Modern Standard Arabic, Western digits)." : "English.");

    private static IReadOnlyList<AiTool> Tools()
    {
        static object Text(string description) => new { type = "string", description };
        static object Number(string description) => new { type = "integer", description };
        const string DateHelp = "today, yesterday, tomorrow or yyyy-mm-dd; default today";
        return
        [
            new("centre_info", "Today's date, the rooms (names, capacity, children per staff member, active children), groups, leave types, staff roles, job titles, enquiry stages and sources, and the branches of the group.",
                new { type = "object", properties = new { } }),
            new("count_children", "Count children, optionally on a date and filtered by room, group, gender, attendance, enrolment status and age; optionally broken down.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        date = Text(DateHelp),
                        room = Text("room name"),
                        group = Text("group name"),
                        gender = Text("female or male"),
                        attendance = Text("present (includes late and left early), absent, late, early_leave, not_checked_in or any"),
                        enrolment_status = Text("active (default), signed_up, on_hold, inactive, withdrawn, transferred or any"),
                        min_age_months = Number("youngest age in months, inclusive"),
                        max_age_months = Number("oldest age in months, inclusive"),
                        group_by = Text("room, group, gender, attendance, age_band or nationality"),
                    },
                }),
            new("count_staff", "Count active staff on a date, filtered by the room they work in that day, role (for example teacher), job title, gender, attendance or being on leave; optionally broken down.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        date = Text(DateHelp),
                        room = Text("room name (staff rostered in that room on that weekday)"),
                        role = Text("system role, for example teacher, centre_admin, receptionist, accounts"),
                        job_title = Text("words in the job title"),
                        gender = Text("female or male"),
                        attendance = Text("present, absent, late, early_leave, not_checked_in or any"),
                        on_leave = new { type = "boolean", description = "true for staff on approved leave that day, false for staff not on leave" },
                        status = Text("employment status: active (default), on_leave, inactive, terminated or any"),
                        group_by = Text("role, job_title, room, gender, attendance, on_leave, status or hire_year"),
                    },
                }),
            new("count_leave", "Count leave requests (and the people on them) overlapping a date or a date range.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        date = Text("a single day: " + DateHelp),
                        from = Text("start of a range, yyyy-mm-dd"),
                        to = Text("end of a range, yyyy-mm-dd"),
                        person = Text("staff, children or all (default all)"),
                        status = Text("approved (default), submitted, rejected, cancelled or any"),
                        leave_type = Text("leave type name"),
                        role = Text("staff role, for example teacher"),
                        group_by = Text("person, leave_type, status or role"),
                    },
                }),
            new("money", "Money totals in AED: billed (invoices issued), collected (payments), outstanding (still owed), overdue (past due date) or expenses, for a date range (default this month).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        measure = Text("billed, collected, outstanding, overdue or expenses"),
                        from = Text("yyyy-mm-dd; default the first day of this month"),
                        to = Text("yyyy-mm-dd; default today"),
                        group_by = Text("month, category (expenses) or status (invoices)"),
                    },
                }),
            new("count_enquiries", "Count enquiries from families in a date range (default the last 90 days), by stage and source.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        from = Text("yyyy-mm-dd"),
                        to = Text("yyyy-mm-dd"),
                        stage = Text("no_stage, interested, contacted, tour_appointment, toured or enrolled"),
                        source = Text("where the enquiry came from"),
                        group_by = Text("stage, source or month"),
                    },
                }),
            new("branches", "This month's figures for every branch of the group: children, staff, check-ins today, joiners and leavers, money and open enquiries.",
                new { type = "object", properties = new { } }),
            new("rooms", "Each active room on a date: capacity, age range, free places, occupancy, children present, absent and in the room now, staff rostered, present and on leave, and whether the staff ratio is met.",
                new { type = "object", properties = new { date = Text(DateHelp), room = Text("one room name; default every room") } }),
            new("curriculum", "The curriculum frameworks: areas, age bands, milestones, how many milestones are planned in lessons and seen in observations, lessons, lesson templates and assessment templates. With a framework, milestones broken down by area and age band.",
                new { type = "object", properties = new { framework = Text("framework name or code; default every framework") } }),
            new("count_lessons", "Count lesson plans, filtered by framework, development area, age band, room and template or not; optionally by when they were written; optionally broken down.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        framework = Text("framework name or code"),
                        area = Text("development area name"),
                        age_band = Text("age band name"),
                        room = Text("room name"),
                        templates = new { type = "boolean", description = "true for reusable lesson templates only, false for lessons only" },
                        from = Text("written on or after, yyyy-mm-dd"),
                        to = Text("written on or before, yyyy-mm-dd"),
                        group_by = Text("framework, area, age_band, room, template or month"),
                    },
                }),
            new("count_observations", "Count observations of children in a date range (default this month), by type (formal, informal, concern), framework, area, age band, the child's room and gender, and shared with parents or not. Also children observed and active children with none.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        from = Text("yyyy-mm-dd; default the first day of this month"),
                        to = Text("yyyy-mm-dd; default today"),
                        type = Text("formal, informal or concern"),
                        framework = Text("framework name or code"),
                        area = Text("development area name"),
                        age_band = Text("age band name"),
                        room = Text("room name"),
                        gender = Text("female or male"),
                        shared_with_parents = new { type = "boolean", description = "true for observations shared with parents, false for staff-only" },
                        group_by = Text("type, framework, area, age_band, room, gender, shared or month"),
                    },
                }),
            new("count_assessments", "Count assessments of children (all time unless a range is given), by framework, age band, term, status, template, approval for parents, the child's room and gender; optionally with milestone scores by area (secure, emerging, not yet).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        from = Text("assessed on or after, yyyy-mm-dd"),
                        to = Text("assessed on or before, yyyy-mm-dd"),
                        framework = Text("framework name or code"),
                        age_band = Text("age band name"),
                        area = Text("development area name: limits the milestone scores to that area"),
                        room = Text("room name"),
                        gender = Text("female or male"),
                        term = Text("term exactly as recorded, for example Autumn 2026"),
                        status = Text("draft or completed"),
                        template = Text("assessment template name"),
                        parent_approved = new { type = "boolean", description = "true for assessments approved to show parents" },
                        include_scores = new { type = "boolean", description = "true to add milestone scores by area" },
                        group_by = Text("status, term, template, room, gender, parent_approved or month"),
                    },
                }),
        ];
    }
}
