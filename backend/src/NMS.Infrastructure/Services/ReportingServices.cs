using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NMS.Application.Billing;
using NMS.Application.Notifications;
using NMS.Application.Reporting;
using NMS.Shared.Results;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NMS.Infrastructure.Services;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 25;

    public string? From { get; set; }

    public string? FromName { get; set; }

    public string? User { get; set; }

    public string? Password { get; set; }

    public bool UseTls { get; set; }
}

/// <summary>Sends e-mail through the configured SMTP server (a local test inbox during development).</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.Host) && !string.IsNullOrWhiteSpace(options.Value.From);

    public async Task<Result> SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return Result.Failure(Error.InvalidState("E-mail is not set up."));
        }

        var o = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName ?? "Bright Futures", o.From!));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(o.Host!, o.Port, o.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
            if (!string.IsNullOrWhiteSpace(o.User))
            {
                await client.AuthenticateAsync(o.User, o.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("E-mail delivery failed: {Type}", ex.GetType().Name);
            return Result.Failure(Error.InvalidState("The e-mail could not be sent."));
        }
    }
}

/// <summary>QuestPDF drawing of the emergency sheet: one page per child, readable in English or Arabic (right to left).</summary>
public sealed class QuestEmergencyReportRenderer : IEmergencyReportRenderer
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["en"] = new()
        {
            ["dob"] = "Date of birth", ["room"] = "Room", ["allergies"] = "Allergies", ["medical"] = "Medical notes", ["dietary"] = "Dietary needs",
            ["language"] = "Language", ["contacts"] = "Contacts", ["none"] = "None recorded", ["pickup"] = "May collect", ["noPickup"] = "May NOT collect",
            ["primary"] = "Primary contact", ["generated"] = "Printed", ["confidential"] = "Confidential: for emergency use only",
        },
        ["ar"] = new()
        {
            ["dob"] = "تاريخ الميلاد", ["room"] = "الغرفة", ["allergies"] = "الحساسية", ["medical"] = "ملاحظات طبية", ["dietary"] = "احتياجات غذائية",
            ["language"] = "اللغة", ["contacts"] = "جهات الاتصال", ["none"] = "لا شيء مسجّل", ["pickup"] = "يحق له الاستلام", ["noPickup"] = "لا يحق له الاستلام",
            ["primary"] = "جهة الاتصال الرئيسية", ["generated"] = "تاريخ الطباعة", ["confidential"] = "سري: للاستخدام في الطوارئ فقط",
        },
    };

    static QuestEmergencyReportRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(EmergencyReportData data)
    {
        var l = Labels[data.Language];
        var rtl = data.Language == "ar";
        return Document.Create(document =>
        {
            foreach (var child in data.Children)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(11));
                    if (rtl)
                    {
                        page.ContentFromRightToLeft();
                    }

                    page.Header().Column(h =>
                    {
                        h.Item().Text(data.CentreName).Bold().FontSize(10).FontColor(Colors.Grey.Darken1);
                        h.Item().Text(data.Title).Bold().FontSize(18).FontColor(Colors.Red.Darken2);
                        h.Item().Text(l["confidential"]).FontSize(9).FontColor(Colors.Red.Darken2);
                    });
                    page.Content().PaddingVertical(10).Column(c =>
                    {
                        c.Spacing(6);
                        c.Item().Text(child.Name).Bold().FontSize(20);
                        if (child.ArabicName is not null)
                        {
                            c.Item().Text(child.ArabicName).FontSize(14);
                        }

                        c.Item().Text($"{l["dob"]}: {child.DateOfBirth:yyyy-MM-dd}   {l["room"]}: {child.Room ?? "-"}   {l["language"]}: {child.PrimaryLanguage ?? "-"}");
                        Block(c, l["allergies"], child.Allergies ?? l["none"], child.Allergies is not null, Colors.Red.Lighten4);
                        Block(c, l["medical"], child.Medical ?? l["none"], child.Medical is not null, Colors.Orange.Lighten4);
                        Block(c, l["dietary"], child.Dietary ?? l["none"], child.Dietary is not null, Colors.Yellow.Lighten4);
                        c.Item().PaddingTop(6).Text(l["contacts"]).Bold().FontSize(13);
                        foreach (var contact in child.Contacts)
                        {
                            c.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(k =>
                            {
                                k.Item().Text($"{contact.Name} ({contact.Relationship}){(contact.IsPrimary ? " - " + l["primary"] : "")}").Bold();
                                k.Item().Text($"{contact.Phone ?? "-"}   {contact.Email}");
                                k.Item().Text(contact.CanPickup ? l["pickup"] : l["noPickup"]).FontColor(contact.CanPickup ? Colors.Green.Darken2 : Colors.Red.Darken2);
                            });
                        }

                        if (child.Contacts.Count == 0)
                        {
                            c.Item().Text(l["none"]);
                        }
                    });
                    page.Footer().AlignCenter().Text($"{l["generated"]}: {data.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            }
        }).GeneratePdf();
    }

    private static void Block(ColumnDescriptor c, string title, string text, bool highlight, string colour) =>
        c.Item().Background(highlight ? colour : Colors.Grey.Lighten4).Padding(6).Column(b =>
        {
            b.Item().Text(title).Bold().FontSize(10);
            b.Item().Text(text);
        });
}

/// <summary>QuestPDF drawing of an invoice in English or Arabic (right to left), amounts shown in AED.</summary>
public sealed class QuestInvoiceRenderer : IInvoiceRenderer
{
    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new()
    {
        ["en"] = new()
        {
            ["invoice"] = "Invoice", ["number"] = "Invoice number", ["child"] = "Child", ["parent"] = "Parent / guardian", ["issued"] = "Issue date", ["due"] = "Due date",
            ["period"] = "Period", ["description"] = "Description", ["qty"] = "Qty", ["unit"] = "Unit price", ["tax"] = "Tax", ["total"] = "Total", ["subtotal"] = "Subtotal",
            ["discount"] = "Discount", ["vat"] = "Tax", ["grand"] = "Total due", ["paid"] = "Paid", ["balance"] = "Balance", ["notes"] = "Notes", ["status"] = "Status",
            ["draft"] = "Draft", ["issued_s"] = "Issued", ["partially_paid"] = "Partially paid", ["paid_s"] = "Paid", ["overdue"] = "Overdue", ["cancelled"] = "Cancelled", ["aed"] = "AED",
        },
        ["ar"] = new()
        {
            ["invoice"] = "فاتورة", ["number"] = "رقم الفاتورة", ["child"] = "الطفل", ["parent"] = "ولي الأمر", ["issued"] = "تاريخ الإصدار", ["due"] = "تاريخ الاستحقاق",
            ["period"] = "الفترة", ["description"] = "البيان", ["qty"] = "الكمية", ["unit"] = "سعر الوحدة", ["tax"] = "الضريبة", ["total"] = "الإجمالي", ["subtotal"] = "المجموع الفرعي",
            ["discount"] = "الخصم", ["vat"] = "الضريبة", ["grand"] = "الإجمالي المستحق", ["paid"] = "المدفوع", ["balance"] = "المتبقي", ["notes"] = "ملاحظات", ["status"] = "الحالة",
            ["draft"] = "مسودة", ["issued_s"] = "صادرة", ["partially_paid"] = "مدفوعة جزئياً", ["paid_s"] = "مدفوعة", ["overdue"] = "متأخرة", ["cancelled"] = "ملغاة", ["aed"] = "درهم",
        },
    };

    static QuestInvoiceRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    private static string Money(long fils) => (fils / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

    public byte[] Render(InvoicePdfData data)
    {
        var l = Labels[data.Language];
        var status = data.Status switch { "paid" => l["paid_s"], "issued" => l["issued_s"], var s => l.GetValueOrDefault(s, s) };
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10.5f));
            if (data.Language == "ar")
            {
                page.ContentFromRightToLeft();
            }

            page.Header().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text(data.CentreName).Bold().FontSize(15);
                    c.Item().Text($"{l["invoice"]} {data.InvoiceNumber}").FontSize(12).FontColor(Colors.Grey.Darken2);
                });
                r.ConstantItem(110).AlignRight().Text(status).Bold().FontSize(13).FontColor(data.Status == "paid" ? Colors.Green.Darken2 : data.Status == "overdue" ? Colors.Red.Darken2 : Colors.Orange.Darken2);
            });
            page.Content().PaddingVertical(14).Column(c =>
            {
                c.Spacing(8);
                c.Item().Text($"{l["child"]}: {data.StudentName}").Bold();
                if (data.Guardians.Count > 0)
                {
                    c.Item().Text($"{l["parent"]}: {string.Join(", ", data.Guardians)}");
                }

                c.Item().Text($"{l["issued"]}: {data.IssueDate:yyyy-MM-dd}    {l["due"]}: {data.DueDate:yyyy-MM-dd}    {l["period"]}: {data.PeriodStart:yyyy-MM-dd} - {data.PeriodEnd:yyyy-MM-dd}");
                c.Item().PaddingTop(6).Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(5);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                    });
                    t.Header(h =>
                    {
                        foreach (var title in new[] { l["description"], l["qty"], l["unit"], l["tax"], l["total"] })
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(title).Bold();
                        }
                    });
                    foreach (var line in data.Lines)
                    {
                        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(line.Description);
                        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(line.Quantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Money(line.UnitPriceFils));
                        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Money(line.TaxFils));
                        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Money(line.TotalFils));
                    }
                });
                c.Item().AlignRight().Width(220).Column(s =>
                {
                    s.Item().Row(r => { r.RelativeItem().Text(l["subtotal"]); r.ConstantItem(90).AlignRight().Text($"{l["aed"]} {Money(data.SubtotalFils)}"); });
                    if (data.DiscountFils > 0)
                    {
                        s.Item().Row(r => { r.RelativeItem().Text(l["discount"]); r.ConstantItem(90).AlignRight().Text($"- {Money(data.DiscountFils)}"); });
                    }

                    s.Item().Row(r => { r.RelativeItem().Text(l["vat"]); r.ConstantItem(90).AlignRight().Text(Money(data.TaxFils)); });
                    s.Item().BorderTop(1).PaddingTop(3).Row(r => { r.RelativeItem().Text(l["grand"]).Bold(); r.ConstantItem(90).AlignRight().Text($"{l["aed"]} {Money(data.TotalFils)}").Bold(); });
                    s.Item().Row(r => { r.RelativeItem().Text(l["paid"]); r.ConstantItem(90).AlignRight().Text(Money(data.PaidFils)); });
                    s.Item().Row(r => { r.RelativeItem().Text(l["balance"]).Bold(); r.ConstantItem(90).AlignRight().Text($"{l["aed"]} {Money(data.TotalFils - data.PaidFils)}").Bold(); });
                });
                if (!string.IsNullOrWhiteSpace(data.Notes))
                {
                    c.Item().PaddingTop(8).Text($"{l["notes"]}: {data.Notes}");
                }
            });
            page.Footer().AlignCenter().Text(data.CentreName).FontSize(8).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();
    }
}
