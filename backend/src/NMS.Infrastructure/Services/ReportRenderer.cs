using System.Text.Json;
using ClosedXML.Excel;
using NMS.Application.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NMS.Infrastructure.Services;

/// <summary>Turns a report table into an Excel workbook, a PDF or JSON.</summary>
public sealed class ReportRenderer : IReportRenderer
{
    static ReportRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public RenderedReport Render(ReportTable table, string format) => format switch
    {
        "excel" => new RenderedReport(Excel(table), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
        "pdf" => new RenderedReport(Pdf(table), "application/pdf", "pdf"),
        "json" => new RenderedReport(Json(table), "application/json", "json"),
        _ => throw new InvalidOperationException($"Unknown format {format}."),
    };

    private static byte[] Excel(ReportTable t)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(t.Title.Length > 31 ? t.Title[..31] : t.Title);
        sheet.RightToLeft = t.Language == "ar";
        var row = 1;
        sheet.Cell(row, 1).SetValue(t.Title).Style.Font.SetBold().Font.SetFontSize(14);
        row++;
        if (t.Subtitle is not null)
        {
            sheet.Cell(row, 1).SetValue(t.Subtitle);
            row++;
        }

        foreach (var (label, value) in t.Summary)
        {
            sheet.Cell(row, 1).SetValue(label).Style.Font.SetBold();
            sheet.Cell(row, 2).SetValue(value);
            row++;
        }

        row++;
        var header = row;
        for (var c = 0; c < t.Columns.Count; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            cell.SetValue(t.Columns[c]);
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E2E8F0"));
        }

        foreach (var r in t.Rows)
        {
            row++;
            for (var c = 0; c < r.Count; c++)
            {
                var cell = sheet.Cell(row, c + 1);
                switch (r[c])
                {
                    case null: break;
                    case int i: cell.SetValue(i); break;
                    case long l: cell.SetValue(l); break;
                    case decimal d: cell.SetValue(d); break;
                    case double d: cell.SetValue(d); break;
                    // Text stays text, so a cell that starts with = or + is never run as a formula.
                    default: cell.SetValue(r[c]!.ToString() ?? string.Empty); cell.Style.NumberFormat.SetFormat("@"); break;
                }
            }
        }

        if (t.Rows.Count > 0)
        {
            sheet.Range(header, 1, row, t.Columns.Count).SetAutoFilter();
        }

        sheet.SheetView.FreezeRows(header);
        sheet.Columns().AdjustToContents(1, Math.Min(row, 200));
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] Pdf(ReportTable t) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(t.Columns.Count > 5 ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));
            if (t.Language == "ar")
            {
                page.ContentFromRightToLeft();
            }

            page.Header().Column(c =>
            {
                c.Item().Text(t.Title).Bold().FontSize(16);
                if (t.Subtitle is not null)
                {
                    c.Item().Text(t.Subtitle).FontColor(Colors.Grey.Darken2);
                }

                foreach (var (label, value) in t.Summary)
                {
                    c.Item().Text(x =>
                    {
                        x.Span($"{label}: ").SemiBold();
                        x.Span(value);
                    });
                }

                c.Item().PaddingBottom(6);
            });
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    for (var i = 0; i < t.Columns.Count; i++)
                    {
                        cols.RelativeColumn(i == 0 ? 2.2f : 1);
                    }
                });
                table.Header(h =>
                {
                    foreach (var column in t.Columns)
                    {
                        h.Cell().Background("#E2E8F0").Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4).Text(column).Bold();
                    }
                });
                foreach (var r in t.Rows)
                {
                    foreach (var v in r)
                    {
                        // Signature cells are left empty and tall enough to sign in.
                        table.Cell().MinHeight(t.Columns.Count == 6 && t.Rows.Count > 0 && string.IsNullOrEmpty(v?.ToString()) ? 26 : 16).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(v is decimal d ? d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : v?.ToString() ?? string.Empty);
                    }
                }
            });
            page.Footer().AlignCenter().Text(x =>
            {
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        })).GeneratePdf();

    private static byte[] Json(ReportTable t)
    {
        var items = t.Rows.Select(r => t.Columns.Select((c, i) => (c, v: i < r.Count ? r[i] : null)).ToDictionary(x => x.c, x => x.v));
        return JsonSerializer.SerializeToUtf8Bytes(new { title = t.Title, subtitle = t.Subtitle, summary = t.Summary.ToDictionary(s => s.Label, s => s.Value), rows = items }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}
