using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NMS.Domain.Billing;

namespace NMS.Infrastructure.Persistence.Configurations;

public sealed class FeePlanConfiguration : IEntityTypeConfiguration<FeePlan>
{
    public void Configure(EntityTypeBuilder<FeePlan> builder)
    {
        builder.ToTable("FeePlans");
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Period).HasMaxLength(20).IsRequired();
        builder.HasOne<NMS.Domain.CentreManagement.Program>().WithMany().HasForeignKey(p => p.ProgramId);
        builder.HasMany(p => p.Components).WithOne().HasForeignKey(c => c.FeePlanId);
        builder.HasIndex(p => new { p.TenantId, p.CreatedAt });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_FeePlans_Period", "[Period] IN (N'monthly', N'termly', N'annual', N'one_time')");
            t.HasCheckConstraint("CK_FeePlans_Status", "[Status] IN (N'active', N'inactive')");
        });
    }
}

public sealed class FeeComponentConfiguration : IEntityTypeConfiguration<FeeComponent>
{
    public void Configure(EntityTypeBuilder<FeeComponent> builder)
    {
        builder.ToTable("FeeComponents");
        builder.Property(c => c.ComponentType).HasMaxLength(20).IsRequired();
        builder.Property(c => c.TaxRatePercent).HasPrecision(5, 2);
        builder.HasIndex(c => new { c.TenantId, c.FeePlanId });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_FeeComponents_Amount", "[AmountFils] >= 0");
            t.HasCheckConstraint("CK_FeeComponents_Tax", "[TaxRatePercent] IS NULL OR ([TaxRatePercent] >= 0 AND [TaxRatePercent] <= 100)");
        });
    }
}

public sealed class StudentFeePlanConfiguration : IEntityTypeConfiguration<StudentFeePlan>
{
    public void Configure(EntityTypeBuilder<StudentFeePlan> builder)
    {
        builder.ToTable("StudentFeePlans");
        builder.HasKey(s => new { s.StudentId, s.FeePlanId, s.EffectiveFrom });
        builder.HasOne<NMS.Domain.CentreManagement.Student>().WithMany().HasForeignKey(s => s.StudentId);
        builder.HasOne<FeePlan>().WithMany().HasForeignKey(s => s.FeePlanId);
        builder.HasIndex(s => new { s.TenantId, s.StudentId });
    }
}

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.Ignore(i => i.OutstandingFils);
        builder.Property(i => i.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(i => i.Status).HasMaxLength(20).IsRequired();
        builder.HasOne<NMS.Domain.CentreManagement.Student>().WithMany().HasForeignKey(i => i.StudentId);
        builder.HasOne<FeePlan>().WithMany().HasForeignKey(i => i.FeePlanId);
        builder.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId);
        builder.HasMany(i => i.Payments).WithOne().HasForeignKey(p => p.InvoiceId);
        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.Status });
        builder.HasIndex(i => new { i.TenantId, i.StudentId, i.IssueDate });
        builder.HasIndex(i => new { i.TenantId, i.DueDate });
        builder.HasIndex(i => new { i.TenantId, i.CreatedAt });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Invoices_Status", "[Status] IN (N'draft', N'issued', N'partially_paid', N'paid', N'overdue', N'cancelled')");
            t.HasCheckConstraint("CK_Invoices_Totals", "[TotalFils] >= 0 AND [PaidFils] >= 0 AND [PaidFils] <= [TotalFils]");
            t.HasCheckConstraint("CK_Invoices_Period", "[PeriodEnd] >= [PeriodStart]");
        });
    }
}

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.Property(l => l.Quantity).HasPrecision(10, 2);
        builder.HasOne<FeeComponent>().WithMany().HasForeignKey(l => l.FeeComponentId);
        builder.HasIndex(l => new { l.TenantId, l.InvoiceId });
    }
}

public sealed class InvoicePaymentConfiguration : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        builder.ToTable("InvoicePayments");
        builder.Property(p => p.Method).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(500);
        builder.HasIndex(p => new { p.TenantId, p.InvoiceId });
        builder.HasIndex(p => new { p.TenantId, p.PaymentDate });
        builder.ToTable(t => t.HasCheckConstraint("CK_InvoicePayments_Amount", "[AmountFils] > 0"));
    }
}
