using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NMS.Application.Abstractions;
using NMS.Domain.Auditing;
using NMS.Domain.Common;

namespace NMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Runs before every save: stamps tenant and who/when, turns deletes of soft-deletable rows into soft deletes,
/// and writes one append-only audit record per changed row in the same transaction (spec 07 §1.1, §7.3).
/// Sensitive fields are recorded as "[encrypted]", never as values.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ITenantContext tenant, ICurrentUser user, IClock clock) : SaveChangesInterceptor
{
    private const string Masked = "[encrypted]";

    private static readonly HashSet<string> NoiseProperties =
        ["UpdatedAt", "UpdatedBy", "CreatedAt", "CreatedBy", "DeletedAt"];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.UtcNow;
        var actor = user.IsAuthenticated ? user.UserId : Guid.Empty;
        var audits = new List<AuditLog>();

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is ITenantEntity and not AuditLog
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var action = Stamp(entry, now, actor);
            if (action is null)
            {
                continue;
            }

            var changes = action == AuditActions.Update ? ChangedValues(entry) : action == AuditActions.Create ? AllValues(entry) : null;
            if (action == AuditActions.Update && changes!.Count == 0)
            {
                continue;
            }

            audits.Add(new AuditLog
            {
                TenantId = entry.Entity is ITenantEntity te && te.TenantId != Guid.Empty ? te.TenantId : tenant.TenantId,
                UserId = user.IsAuthenticated ? user.UserId : null,
                UserEmail = user.Email,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = EntityId(entry),
                Action = action,
                ChangedFields = changes,
                IpAddress = user.IpAddress,
                UserAgent = user.UserAgent,
                RequestId = user.RequestId,
                OccurredAt = now,
            });
        }

        if (audits.Count > 0)
        {
            context.Set<AuditLog>().AddRange(audits);
        }
    }

    /// <summary>Stamps the row and returns the audit action, or null when nothing should be audited.</summary>
    private string? Stamp(EntityEntry entry, DateTimeOffset now, Guid actor)
    {
        if (entry.Entity is ITenantEntity tenantEntity && entry.State == EntityState.Added && tenantEntity.TenantId == Guid.Empty)
        {
            tenantEntity.TenantId = tenant.TenantId;
        }

        if (entry.Entity is IAuditable auditable)
        {
            if (entry.State == EntityState.Added)
            {
                auditable.CreatedAt = now;
                auditable.UpdatedAt = now;
                if (auditable.CreatedBy == Guid.Empty)
                {
                    auditable.CreatedBy = actor;
                }

                if (auditable.UpdatedBy == Guid.Empty)
                {
                    auditable.UpdatedBy = actor;
                }
            }
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                auditable.UpdatedAt = now;
                auditable.UpdatedBy = actor;
            }
        }

        if (entry.Entity is ISoftDeletable softDeletable && entry.State == EntityState.Deleted)
        {
            entry.State = EntityState.Modified;
            softDeletable.IsDeleted = true;
            softDeletable.DeletedAt = now;
            return AuditActions.Delete;
        }

        return entry.State switch
        {
            EntityState.Added => AuditActions.Create,
            EntityState.Deleted => AuditActions.Delete,
            _ => AuditActions.Update,
        };
    }

    private static Dictionary<string, AuditFieldChange> AllValues(EntityEntry entry) =>
        entry.Properties
            .Where(p => !NoiseProperties.Contains(p.Metadata.Name) && p.CurrentValue is not null)
            .ToDictionary(p => p.Metadata.Name, p => new AuditFieldChange(null, Display(p)));

    private static Dictionary<string, AuditFieldChange> ChangedValues(EntityEntry entry) =>
        entry.Properties
            .Where(p => p.IsModified && !NoiseProperties.Contains(p.Metadata.Name)
                && !Equals(p.OriginalValue, p.CurrentValue))
            .ToDictionary(p => p.Metadata.Name, p => new AuditFieldChange(Display(p, original: true), Display(p)));

    private static object? Display(PropertyEntry property, bool original = false)
    {
        var value = original ? property.OriginalValue : property.CurrentValue;
        if (value is null)
        {
            return null;
        }

        var sensitive = property.Metadata.PropertyInfo?.GetCustomAttribute<SensitiveAttribute>() is not null;
        return sensitive ? Masked : value is DateOnly or TimeOnly or DateTimeOffset or Guid ? value.ToString() : value;
    }

    private static Guid EntityId(EntityEntry entry)
    {
        if (entry.Entity is Entity entity)
        {
            return entity.Id;
        }

        // Join tables have composite keys; the first Guid in the key identifies the owning row.
        var key = entry.Metadata.FindPrimaryKey();
        var first = key?.Properties.Select(p => entry.Property(p.Name).CurrentValue).OfType<Guid>().FirstOrDefault();
        return first ?? Guid.Empty;
    }
}
