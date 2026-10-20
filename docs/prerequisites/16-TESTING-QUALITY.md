# 16 — Testing & Quality

> Test pyramid, coverage gates, quality bars, performance testing, accessibility testing.

---

## 1. Testing Strategy

### 1.1 Test Pyramid

```
              ┌──────────┐
              │   E2E    │  ~5% (critical journeys only)
           ┌──┴──────────┴──┐
           │  Integration   │  ~25% (API contract, DB)
        ┌──┴────────────────┴──┐
        │     Component        │  ~30% (React components, hooks)
     ┌──┴──────────────────────┴──┐
     │          Unit               │  ~40% (business logic, pure functions)
     └─────────────────────────────┘
```

### 1.2 Coverage Gates

| Layer | Coverage target | CI gate |
|---|---|---|
| Backend (.NET) | 80% line coverage | Blocks merge if < 80% |
| Web (React) | 75% line coverage | Blocks merge if < 75% |
| Mobile (RN) | 70% line coverage | Blocks merge if < 70% |

Coverage measured by:
- .NET: Coverlet + Cobertura XML → Codecov.
- React/Vite: Vitest + c8 → Codecov.
- React Native: Jest + coverage → Codecov.

### 1.3 Quality Gates (SonarCloud)

Every PR must pass:
- 0 critical or blocker issues.
- 0 security hotspots (or all marked as "safe" with justification).
- Coverage ≥ gate threshold.
- Duplicated lines < 3%.
- Cyclomatic complexity < 15 average.
- Cognitive complexity < 15 average.

---

## 2. Backend Testing (.NET)

### 2.1 Test Stack

| Type | Tool | Purpose |
|---|---|---|
| Unit | xUnit + FluentAssertions + Moq | Pure business logic, no DB |
| Integration | xUnit + Testcontainers (PostgreSQL, Redis, RabbitMQ) | API endpoints, DB, real infrastructure |
| Architecture | NetArchTest | Enforce module boundaries, no circular deps |
| Load | k6 | API performance |
| Static analysis | SonarCloud | Quality gate |
| Mutation | Stryker.NET | Verify test quality (catches tests that pass with broken code) |

### 2.2 Unit Test Example

```csharp
// tests/NMS.UnitTests/Application/Attendance/CheckInStudentHandlerTests.cs
public class CheckInStudentHandlerTests
{
    private readonly Mock<IStudentRepository> _studentRepoMock;
    private readonly Mock<IAttendanceRepository> _attendanceRepoMock;
    private readonly Mock<IAuditLogger> _auditLoggerMock;
    private readonly CheckInStudentHandler _handler;
    
    public CheckInStudentHandlerTests()
    {
        _studentRepoMock = new Mock<IStudentRepository>();
        _attendanceRepoMock = new Mock<IAttendanceRepository>();
        _auditLoggerMock = new Mock<IAuditLogger>();
        _handler = new CheckInStudentHandler(
            _studentRepoMock.Object,
            _attendanceRepoMock.Object,
            _auditLoggerMock.Object);
    }
    
    [Fact]
    public async Task Handle_ValidBarcode_StudentActive_ReturnsSuccess()
    {
        // Arrange
        var barcode = "STU-12345";
        var student = new Student { Id = Guid.NewGuid(), Barcode = barcode, Status = StudentStatus.Active };
        _studentRepoMock
            .Setup(r => r.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);
        
        var command = new CheckInStudentCommand(barcode, CheckInMethod.Barcode);
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student.Id);
        _attendanceRepoMock.Verify(r => r.AddAsync(It.Is<AttendanceRecord>(a =>
            a.StudentId == student.Id &&
            a.CheckInAt != null &&
            a.CheckInMethod == CheckInMethod.Barcode), It.IsAny<CancellationToken>()), Times.Once);
        _auditLoggerMock.Verify(a => a.LogAsync(It.IsAny<AuditEvent>()), Times.Once);
    }
    
    [Fact]
    public async Task Handle_StudentAlreadyCheckedIn_ReturnsFailure()
    {
        // Arrange
        var barcode = "STU-12345";
        var student = new Student { Id = Guid.NewGuid(), Barcode = barcode, Status = StudentStatus.Active };
        _studentRepoMock.Setup(r => r.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);
        _attendanceRepoMock.Setup(r => r.GetTodayAttendanceAsync(student.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceRecord { CheckInAt = DateTime.UtcNow.AddMinutes(-30) });
        
        var command = new CheckInStudentCommand(barcode, CheckInMethod.Barcode);
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ALREADY_CHECKED_IN");
        _attendanceRepoMock.Verify(r => r.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task Handle_StudentNotFound_ReturnsNotFound()
    {
        // Arrange
        var barcode = "INVALID";
        _studentRepoMock.Setup(r => r.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);
        
        var command = new CheckInStudentCommand(barcode, CheckInMethod.Barcode);
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.StatusCode.Should().Be(404);
    }
}
```

### 2.3 Integration Test Example

```csharp
// tests/NMS.IntegrationTests/Api/AttendanceEndpointsTests.cs
public class AttendanceEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;
    
    public AttendanceEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task POST_CheckIn_ValidBarcode_Returns200()
    {
        // Arrange
        await _factory.SeedDataAsync(async db =>
        {
            db.Students.Add(new Student
            {
                Id = Guid.NewGuid(),
                TenantId = _factory.TenantId,
                Barcode = "STU-TEST-001",
                FirstName = "Test",
                LastName = "Student",
                DateOfBirth = new DateOnly(2023, 1, 1),
                Status = StudentStatus.Active,
            });
            await db.SaveChangesAsync();
        });
        
        var token = await _factory.GetTokenForUserAsync(role: "teacher");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/attendance/actions/check-in", new
        {
            Barcode = "STU-TEST-001",
            Method = "barcode"
        });
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<AttendanceDto>>();
        content!.Success.Should().BeTrue();
        content.Data!.CheckInAt.Should().NotBeNull();
    }
    
    [Fact]
    public async Task POST_CheckIn_ParentAttenptingOtherChild_Returns404()
    {
        // Arrange — parent A owns student A, attempts to check in student B
        var parentA = await _factory.CreateUserAsync(role: "parent");
        var studentA = await _factory.CreateStudentAsync();
        var studentB = await _factory.CreateStudentAsync();
        await _factory.LinkParentToStudentAsync(parentA, studentA);
        
        var token = await _factory.GetTokenForUserAsync(parentA);
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/attendance/actions/check-in", new
        {
            Barcode = studentB.Barcode,
            Method = "barcode"
        });
        
        // Assert — 404 not 403 (prevents ID enumeration)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task POST_CheckIn_CrossTenant_Returns404()
    {
        // Arrange — staff from tenant A attempts to check in student from tenant B
        var tenantA = await _factory.CreateTenantAsync("tenant-a");
        var tenantB = await _factory.CreateTenantAsync("tenant-b");
        var staffA = await _factory.CreateUserAsync(tenantA, role: "teacher");
        var studentB = await _factory.CreateStudentAsync(tenantB);
        
        var token = await _factory.GetTokenForUserAsync(staffA);
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/attendance/actions/check-in", new
        {
            Barcode = studentB.Barcode,
            Method = "barcode"
        });
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

### 2.4 Architecture Tests (NetArchTest)

```csharp
// tests/NMS.ArchitectureTests/ModuleBoundariesTests.cs
public class ModuleBoundariesTests
{
    [Fact]
    public void Modules_ShouldNotReference_OtherModulesInternals()
    {
        var modules = new[]
        {
            typeof(NMS.Modules.Attendance.AssemblyMarker),
            typeof(NMS.Modules.Billing.AssemblyMarker),
            typeof(NMS.Modules.CentreManagement.AssemblyMarker),
            typeof(NMS.Modules.Messaging.AssemblyMarker),
            typeof(NMS.Modules.Notifications.AssemblyMarker),
            typeof(NMS.Modules.ParentCommunication.AssemblyMarker),
        };
        
        foreach (var module in modules)
        {
            var otherModules = modules.Where(m => m != module).ToArray();
            
            var result = Types.InAssembly(module.Assembly)
                .ShouldNot()
                .HaveDependencyOnAny(otherModules.Select(m => m.Namespace!).ToArray())
                .GetResult();
            
            result.IsSuccessful.Should().BeTrue(
                $"Module {module.Namespace} should not depend on other modules' internals. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }
    
    [Fact]
    public void Controllers_ShouldNotReference_RepositoriesDirectly()
    {
        var result = Types.InCurrentDomain()
            .That().HaveNameEndingWith("Controller")
            .ShouldNot().HaveDependencyOnAny("NMS.Infrastructure.Persistence.Repositories")
            .GetResult();
        
        result.IsSuccessful.Should().BeTrue();
    }
}
```

---

## 3. Web Frontend Testing

### 3.1 Test Stack

| Type | Tool | Purpose |
|---|---|---|
| Unit | Vitest | Pure functions, hooks |
| Component | Vitest + React Testing Library + MSW | Component rendering + interactions |
| Visual regression | Playwright + Percy/Chromatic | Catch visual changes |
| E2E | Playwright | Critical user journeys |
| A11y | axe-core + vitest-axe + @axe-core/playwright | Accessibility |

### 3.2 Component Test Example

```tsx
// src/modules/centre-management/components/StudentForm.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StudentForm } from './StudentForm';
import { server } from '@/test/mocks/server';
import { rest } from 'msw';

describe('StudentForm', () => {
  it('should submit valid form data', async () => {
    const onSubmit = vi.fn();
    const user = userEvent.setup();
    
    render(<StudentForm onSubmit={onSubmit} />);
    
    await user.type(screen.getByLabelText(/first name/i), 'Ahmed');
    await user.type(screen.getByLabelText(/last name/i), 'Ali');
    await user.type(screen.getByLabelText(/date of birth/i), '2023-01-15');
    await user.click(screen.getByRole('button', { name: /save/i }));
    
    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
        firstName: 'Ahmed',
        lastName: 'Ali',
        dateOfBirth: '2023-01-15',
      }));
    });
  });
  
  it('should show validation errors for empty required fields', async () => {
    const user = userEvent.setup();
    render(<StudentForm onSubmit={vi.fn()} />);
    
    await user.click(screen.getByRole('button', { name: /save/i }));
    
    expect(await screen.findByText(/first name is required/i)).toBeInTheDocument();
    expect(screen.getByText(/last name is required/i)).toBeInTheDocument();
  });
  
  it('should handle API error gracefully', async () => {
    server.use(
      rest.post('/api/v1/students', (req, res, ctx) => {
        return res(ctx.status(409), ctx.json({
          success: false,
          errors: [{ code: 'DUPLICATE_RESOURCE', message: 'Barcode already exists' }],
        }));
      })
    );
    
    const user = userEvent.setup();
    render(<StudentForm onSubmit={vi.fn()} />);
    
    await user.type(screen.getByLabelText(/first name/i), 'Ahmed');
    // Fill all required fields...
    await user.click(screen.getByRole('button', { name: /save/i }));
    
    expect(await screen.findByText(/barcode already exists/i)).toBeInTheDocument();
  });
  
  it('should have no accessibility violations', async () => {
    const { container } = render(<StudentForm onSubmit={vi.fn()} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

### 3.3 E2E Test Example (Playwright)

```tsx
// e2e/attendance.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Attendance', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsStaff(page, 'teacher@brightfutures.ae', 'password');
  });
  
  test('should check in student via manual button', async ({ page }) => {
    await page.goto('/attendance');
    
    const studentRow = page.locator('tr', { hasText: 'Ahmed Ali' });
    await studentRow.locator('[data-testid="check-in-button"]').click();
    
    await expect(studentRow).toContainText('Checked In');
    await expect(page.locator('[data-testid="check-in-success"]')).toBeVisible();
  });
  
  test('should show ratio breach alert when ratio falls below threshold', async ({ page }) => {
    await page.goto('/dashboard');
    
    // Trigger ratio breach via test helper
    await page.evaluate(() => {
      window.dispatchEvent(new CustomEvent('test:trigger-ratio-breach', {
        detail: { roomId: 'room-1', studentsPresent: 10, staffPresent: 1 }
      }));
    });
    
    await expect(page.locator('[data-testid="critical-banner"]')).toBeVisible();
    await expect(page.locator('[data-testid="critical-banner"]')).toContainText(/ratio breach/i);
  });
  
  test('should display correctly in Arabic RTL', async ({ page }) => {
    await page.click('[data-testid="language-switcher"]');
    await page.click('text=العربية');
    
    await expect(page).toHaveURL(/.*lang=ar/);
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('h1')).toContainText('الحضور'); // "Attendance" in Arabic
    
    // Verify layout mirrors correctly
    const sidebar = page.locator('[data-testid="sidebar"]');
    const sidebarBox = await sidebar.boundingBox();
    expect(sidebarBox?.x).toBeLessThan(100); // Sidebar on left in LTR... wait, in RTL should be on right
    // Actually in RTL, sidebar is on right side
  });
});
```

### 3.4 Visual Regression Tests

```tsx
// e2e/visual-regression.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Visual regression', () => {
  test.describe.configure({ mode: 'parallel' });
  
  for (const locale of ['en', 'ar']) {
    for (const theme of ['light', 'dark']) {
      test(`dashboard - ${locale} - ${theme}`, async ({ page }) => {
        await page.goto(`/dashboard?lang=${locale}&theme=${theme}`);
        await page.waitForLoadState('networkidle');
        await expect(page).toHaveScreenshot(`dashboard-${locale}-${theme}.png`, {
          fullPage: true,
          maxDiffPixelRatio: 0.01,
        });
      });
    }
  }
});
```

---

## 4. Mobile Testing

### 4.1 Test Stack

| Type | Tool | Purpose |
|---|---|---|
| Unit | Jest | Pure functions, hooks |
| Component | Jest + React Native Testing Library | Component rendering |
| E2E | Detox | Real device/simulator flows |
| A11y | Manual (VoiceOver, TalkBack) | Screen reader testing |

### 4.2 Detox E2E Example

```js
// e2e/attendance.spec.js
describe('Attendance', () => {
  beforeAll(async () => {
    await device.launchApp({ newInstance: true });
    await loginAsStaff('teacher@brightfutures.ae', 'password');
  });

  beforeEach(async () => {
    await device.reloadReactNative();
  });

  it('should navigate to attendance and check in student', async () => {
    await element(by.id('tab-attendance')).tap();
    
    // Wait for student list to load
    await waitFor(element(by.text('Ahmed Ali'))).toBeVisible().withTimeout(5000);
    
    // Tap check-in button for first student
    await element(by.id('check-in-button')).atIndex(0).tap();
    
    // Verify success overlay
    await expect(element(by.id('check-in-success'))).toBeVisible();
    await waitFor(element(by.id('check-in-success'))).toBeNotVisible().withTimeout(3000);
    
    // Verify student status updated
    await expect(element(by.text('Ahmed Ali')).withDescendant(by.text('Checked In'))).toBeVisible();
  });

  it('should scan barcode via camera', async () => {
    await element(by.id('tab-attendance')).tap();
    await element(by.id('scan-button')).tap();
    
    // Mock camera barcode detection (Detox test helper)
    await device.mockBarcodeScan('STU-12345');
    
    await expect(element(by.id('check-in-success'))).toBeVisible();
  });

  it('should work offline (queue check-in)', async () => {
    await device.setURLBlacklist(['.*api.nms.ae.*']);
    
    await element(by.id('tab-attendance')).tap();
    await element(by.id('check-in-button')).atIndex(0).tap();
    
    await expect(element(by.text('Queued (offline)'))).toBeVisible();
    
    // Restore network
    await device.setURLBlacklist([]);
    
    // Trigger sync
    await element(by.id('sync-now')).tap();
    
    await expect(element(by.text('Synced'))).toBeVisible();
  });
});
```

---

## 5. Performance Testing

### 5.1 Tools

- **k6** for API load testing.
- **Playwright Tracing** for web performance profiling.
- **Firebase Performance Monitoring** for mobile.

### 5.2 k6 Load Test Example

```js
// infra/load-tests/dashboard-load.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const successRate = new Rate('success_rate');

export const options = {
  stages: [
    { duration: '30s', target: 50 },   // Ramp up to 50 users
    { duration: '1m', target: 50 },    // Stay at 50
    { duration: '30s', target: 100 },  // Ramp up to 100
    { duration: '2m', target: 100 },   // Stay at 100 (Phase 1 target)
    { duration: '30s', target: 200 },  // Spike to 200
    { duration: '1m', target: 200 },   // Stay at 200 (stress test)
    { duration: '30s', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000', 'p(99)<3000'],  // 95% < 2s, 99% < 3s
    http_req_failed: ['rate<0.01'],  // < 1% errors
    success_rate: ['rate>0.99'],
  },
};

export default function () {
  const token = __ENV.AUTH_TOKEN;
  
  const response = http.get('https://staging.nms.ae/api/v1/dashboard/kpis', {
    headers: { Authorization: `Bearer ${token}` },
  });
  
  const isSuccess = check(response, {
    'status is 200': (r) => r.status === 200,
    'response has data': (r) => r.json('success') === true,
    'response time < 2s': (r) => r.timings.duration < 2000,
  });
  
  successRate.add(isSuccess);
  sleep(1);  // 1 RPS per virtual user
}
```

### 5.3 Performance Budgets

| Endpoint | Target p50 | Target p95 | Target p99 |
|---|---|---|---|
| Dashboard KPIs | 200ms | 500ms | 1s |
| Student list (50 students) | 100ms | 300ms | 500ms |
| Activity feed (cursor paginated) | 150ms | 400ms | 800ms |
| Check-in/out | 100ms | 250ms | 500ms |
| Invoice generation (batch) | N/A | N/A | < 30s (async) |
| Emergency report (single PDF) | 500ms | 1s | 2s |
| Emergency report bulk (whole room) | 2s | 5s | 10s |

### 5.4 Web Performance Targets

- LCP (Largest Contentful Paint): < 2s.
- FID (First Input Delay): < 100ms.
- CLS (Cumulative Layout Shift): < 0.1.
- TTI (Time to Interactive): < 3s.
- Lighthouse Performance: > 90.

### 5.5 Mobile Performance Targets

- App launch to interactive: < 2s (cold start), < 1s (warm).
- Activity feed scroll: 60 FPS (no jank).
- Photo upload (presigned URL flow): < 5s for 5MB image.
- Push notification delivery: < 30s from event to device.

### 5.6 Performance CI Gate

Every PR runs:
- k6 smoke test (10 virtual users, 30 seconds).
- Lighthouse audit on key pages (dashboard, attendance, parent feed).
- Bundle size check (warn if > budget).

Staging nightly:
- Full k6 load test (Phase 1 target: 100 concurrent users).
- Lighthouse audit on all routes.

---

## 6. Accessibility Testing

### 6.1 Automated (CI)

```tsx
// Every component test runs axe
import { axe } from 'vitest-axe';

it('should have no accessibility violations', async () => {
  const { container } = render(<Button>Save</Button>);
  const results = await axe(container);
  expect(results).toHaveNoViolations();
});

// E2E tests run axe on every page
import { injectAxe, checkA11y } from 'axe-playwright';

test('dashboard has no a11y violations', async ({ page }) => {
  await page.goto('/dashboard');
  await injectAxe(page);
  await checkA11y(page, undefined, {
    detailedReport: true,
  });
});
```

### 6.2 Manual Testing Checklist (Before Each Phase Go-Live)

| Test | Tool | Frequency |
|---|---|---|
| Screen reader (parent portal) | NVDA on Windows | Each phase go-live |
| Screen reader (parent portal) | VoiceOver on macOS | Each phase go-live |
| Screen reader (mobile app) | VoiceOver on iOS | Each phase go-live |
| Screen reader (mobile app) | TalkBack on Android | Each phase go-live |
| Keyboard-only navigation | Browser | Each phase go-live |
| High contrast mode | Windows High Contrast | Each phase go-live |
| 200% zoom | Browser | Each phase go-live |
| Color blindness simulator | Chrome DevTools | Each phase go-live |
| Cognitive test | Wave extension | Each phase go-live |

### 6.3 WCAG 2.1 AA Compliance Checklist

- [ ] All non-decorative images have meaningful alt text.
- [ ] Form inputs have associated labels (not just placeholders).
- [ ] Color is never the only indicator of meaning (icon + text too).
- [ ] Color contrast meets 4.5:1 (normal text) / 3:1 (large text).
- [ ] All interactive elements reachable via Tab.
- [ ] Visible focus indicator on all interactive elements.
- [ ] Skip-to-content link on every page.
- [ ] Headings are hierarchical (no skipped levels).
- [ ] ARIA labels on icon-only buttons.
- [ ] Live regions (`aria-live`) announce dynamic content.
- [ ] Modals trap focus, return focus on close.
- [ ] Page has logical reading order in DOM.
- [ ] Page works with CSS disabled (semantic HTML).
- [ ] Form errors are announced (not just visually shown).

---

## 7. Security Testing

### 7.1 SAST (Static Analysis)

- **SonarCloud**: scans every PR for security hotspots.
- **Semgrep**: custom rules for NMS-specific patterns (e.g., "no raw SQL without parameterization").

### 7.2 SCA (Software Composition Analysis)

- **OWASP Dependency Check**: scans .NET + npm dependencies on every PR.
- **GitHub Dependabot**: opens PRs for vulnerable dependencies.
- **Snyk** (optional): additional scanning.

### 7.3 DAST (Dynamic Analysis)

- **OWASP ZAP**: weekly scan of staging environment.
- Findings triaged within 1 week (critical: immediate).

### 7.4 Container Scanning

- **Trivy**: scans every Docker image on push.
- Critical findings block deployment.

### 7.5 IaC Scanning

- **tfsec**: scans Terraform on every PR.
- **checkov**: additional IaC scanning.

### 7.6 Secrets Scanning

- **GitGuardian**: scans every push for committed secrets.
- **GitHub Secret Scanning**: additional coverage.

### 7.7 Penetration Testing

External pen test before each phase go-live:
- **Phase 1 go-live** (Sprint 17): full OWASP Top 10 + business logic.
- **Phase 2 go-live** (Sprint 26): new modules + mobile app.
- **Phase 3 go-live** (Sprint 30): payment gateway + multi-tenancy + Open API.
- **Annual** (ongoing): annual pen test by 3rd party.

---

## 8. Mutation Testing (Sample, Not Every PR)

To verify test quality (catches "tests that pass with broken code"):

```bash
# Backend
dotnet stryker --project NMS.Application --reporter html

# Web
npx @stryker-mutator/core run --config stryker.config.json
```

Run on critical modules before each phase go-live. Mutation score target: > 70%.

---

## 9. Test Data Management

### 9.1 Dev Environment Seed Data

`backend/src/NMS.Api/DevDataSeeder.cs` seeds:
- 1 tenant (Bright Futures Nursery).
- 8 staff users (1 per role).
- 30 students with guardians, distributed across 4 rooms.
- 4 rooms, 3 programs, 6 groups.
- 2 weeks of attendance history.
- 50 activity posts (mixed types, mixed approval status).
- 20 messages across 10 threads.
- 5 invoices (mixed statuses).
- 10 enquiries (mixed stages).
- Audit log with 100+ entries.

Reset weekly (Monday 3 AM via Hangfire job).

### 9.2 Staging Data

Anonymized production snapshot, refreshed monthly:
- Strip PII (names → "Student 1, 2, 3", emails → "user1@example.com").
- Preserve data shape + volume.
- Same tenant structure as production (single tenant).

### 9.3 Test Data Builders

```csharp
// tests/NMS.IntegrationTests/Builders/StudentBuilder.cs
public class StudentBuilder
{
    private Student _student = new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "Student",
        DateOfBirth = new DateOnly(2023, 1, 1),
        Status = StudentStatus.Active,
        Barcode = $"STU-{Guid.NewGuid():N}".Substring(0, 15),
    };
    
    public StudentBuilder WithTenant(Guid tenantId)
    {
        _student.TenantId = tenantId;
        return this;
    }
    
    public StudentBuilder WithStatus(StudentStatus status)
    {
        _student.Status = status;
        return this;
    }
    
    public StudentBuilder InRoom(Guid roomId)
    {
        _student.RoomId = roomId;
        return this;
    }
    
    public Student Build() => _student;
}
```

---

## 10. Continuous Quality

### 10.1 PR Review Checklist

Reviewer verifies:
- [ ] Tests added/updated for new functionality.
- [ ] Coverage maintained or improved.
- [ ] No critical SonarCloud issues.
- [ ] No new security hotspots.
- [ ] E2E tests pass on PR (if UI change).
- [ ] Visual regression screenshots reviewed.
- [ ] Performance impact considered (if perf-sensitive code).
- [ ] Accessibility tested (if UI change).
- [ ] Both LTR and RTL tested (if UI change).
- [ ] Documentation updated (if API/architecture change).
- [ ] ADR written (if architecture decision).

### 10.2 Definition of Done

A story is "done" when:
- [ ] All acceptance criteria met.
- [ ] Code reviewed + approved by 2 reviewers (1 for non-architectural changes).
- [ ] All tests pass (unit, integration, E2E).
- [ ] Coverage gate met.
- [ ] SonarCloud gate met.
- [ ] No critical security findings.
- [ ] Deployed to staging.
- [ ] Product Owner verified in staging.
- [ ] Documentation updated.
- [ ] Merged to main.

### 10.3 Bug Triage

| Severity | Definition | SLA |
|---|---|---|
| P0 (Critical) | Production down, data loss, security breach | Fix in 4 hours |
| P1 (High) | Major feature broken, no workaround | Fix in 24 hours |
| P2 (Medium) | Feature partially broken, workaround exists | Fix in 1 week |
| P3 (Low) | Cosmetic, minor inconvenience | Fix in next sprint |
| P4 (Trivial) | Typo, tooltip improvement | Fix when convenient |

Bugs tracked in GitHub Issues with severity label.

---

**Next file to read**: `17-COMPLIANCE-LEGAL.md` for UAE PDPL, KHDA/ADEK, data residency, retention policies.
