using System.Globalization;
using ClosedXML.Excel;
using NMS.Application.Import;
using NMS.Shared.Results;

namespace NMS.Infrastructure;

/// <summary>Excel-backed implementation of <see cref="IImportWorkbook"/> using ClosedXML.</summary>
internal sealed class ClosedXmlImportWorkbook : IImportWorkbook
{
    private const double DefaultColWidth = 24;

    public byte[] BuildTemplate(IReadOnlyList<ImportSheet> sheets, IReadOnlyList<string> readMe)
    {
        using var wb = new XLWorkbook();

        // ── Read-me sheet ───────────────────────────────────────────────────────────
        var rm = wb.Worksheets.Add("Read me");
        for (int i = 0; i < readMe.Count; i++)
        {
            var cell = rm.Cell(i + 1, 1);
            cell.Value = readMe[i];
            if (i == 0)
            {
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 14;
            }
        }
        rm.Column(1).Width = 100;
        rm.SheetView.FreezeRows(0);

        // ── Data sheets ─────────────────────────────────────────────────────────────
        foreach (var sheet in sheets)
        {
            var ws = wb.Worksheets.Add(sheet.Name);

            for (int c = 0; c < sheet.Columns.Count; c++)
            {
                var col = sheet.Columns[c];
                int colNum = c + 1;

                // Header cell
                var hdr = ws.Cell(1, colNum);
                hdr.Value = col.Header;
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
                hdr.Style.Font.FontColor = XLColor.White;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                ws.Column(colNum).Width = DefaultColWidth;

                // Dropdown validation for choice columns
                if (col.Choices is { Count: > 0 } choices)
                {
                    // Inline list formula: "\"option1,option2,...\""
                    var formula = $"\"{string.Join(",", choices)}\"";
                    var dvRange = ws.Range(2, colNum, XLHelper.MaxRowNumber, colNum);
                    var dv = dvRange.CreateDataValidation();
                    dv.List(formula);
                    dv.ShowErrorMessage = true;
                    dv.ErrorTitle = "Invalid value";
                    dv.ErrorMessage = $"Choose one of: {string.Join(", ", choices)}.";
                }
            }

            // Freeze header row
            ws.SheetView.FreezeRows(1);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public Result<IReadOnlyList<WorkbookSheet>> Read(byte[] file)
    {
        try
        {
            using var ms = new MemoryStream(file);
            using var wb = new XLWorkbook(ms);

            var result = new List<WorkbookSheet>();
            foreach (var ws in wb.Worksheets)
            {
                var usedRange = ws.RangeUsed();
                if (usedRange is null)
                    continue;

                int lastCol = usedRange.LastColumn().ColumnNumber();
                int lastRow = usedRange.LastRow().RowNumber();

                // Headers from row 1
                var headers = new List<string>();
                for (int c = 1; c <= lastCol; c++)
                {
                    var val = ws.Cell(1, c).GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(val))
                        headers.Add(val.Trim());
                    else
                        break; // stop at the first empty header
                }

                if (headers.Count == 0)
                    continue;

                // Data rows from row 2 onwards
                var rows = new List<WorkbookRow>();
                for (int r = 2; r <= lastRow; r++)
                {
                    var cells = new Dictionary<string, string>(StringComparer.Ordinal);
                    bool anyValue = false;

                    for (int c = 0; c < headers.Count; c++)
                    {
                        var cell = ws.Cell(r, c + 1);
                        var text = CellText(cell);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            cells[headers[c]] = text.Trim();
                            anyValue = true;
                        }
                    }

                    if (anyValue)
                        rows.Add(new WorkbookRow(r - 1, cells)); // row number is 1-based data row index
                }

                result.Add(new WorkbookSheet(ws.Name, headers, rows));
            }

            return Result<IReadOnlyList<WorkbookSheet>>.Success(result);
        }
        catch (Exception ex)
        {
            return Error.Validation($"The file could not be read as an Excel workbook: {ex.Message}", "file");
        }
    }

    private static string CellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
        XLDataType.Boolean => cell.GetBoolean() ? "true" : "false",
        _ => cell.GetValue<string>(),
    };
}
