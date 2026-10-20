using NMS.Application.Tools;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NMS.Infrastructure.Services;

/// <summary>Draws one ID card per page. Standard size is A6 (105 x 148 mm); a credit-card size (85.6 x 54 mm) is also offered.</summary>
public sealed class QuestIdCardRenderer : IIdCardRenderer
{
    static QuestIdCardRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(IReadOnlyList<IdCardPerson> people, IdCardStyle style) =>
        Document.Create(document =>
        {
            foreach (var person in people)
            {
                document.Page(page =>
                {
                    page.Margin(0);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(style.Size == "cr80" ? 7 : 10));
                    if (style.Size == "cr80")
                    {
                        page.Size(85.6f, 54f, Unit.Millimetre);
                    }
                    else
                    {
                        page.Size(PageSizes.A6);
                    }

                    if (style.Language == "ar")
                    {
                        page.ContentFromRightToLeft();
                    }

                    page.Content().Element(c =>
                    {
                        if (style.Size == "cr80")
                        {
                            Landscape(c, person, style);
                        }
                        else
                        {
                            Portrait(c, person, style);
                        }
                    });
                });
            }
        }).GeneratePdf();

    private static byte[] Qr(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(10);
    }

    private static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    private static string Label(string key, string language) => (language, key) switch
    {
        ("ar", "dob") => "تاريخ الميلاد",
        ("ar", "room") => "الغرفة",
        ("ar", "program") => "البرنامج",
        ("ar", "expiry") => "ينتهي في",
        (_, "dob") => "Date of birth",
        (_, "room") => "Room",
        (_, "program") => "Programme",
        (_, "expiry") => "Expires",
        _ => key,
    };

    private static IEnumerable<(string Label, string Value)> Details(IdCardPerson p, IdCardStyle s)
    {
        if (s.Fields.Contains("dob") && p.DateOfBirth is { } dob) yield return (Label("dob", s.Language), dob.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture));
        if (s.Fields.Contains("room") && !string.IsNullOrWhiteSpace(p.Room)) yield return (Label("room", s.Language), p.Room!);
        if (s.Fields.Contains("program") && !string.IsNullOrWhiteSpace(p.Program)) yield return (Label("program", s.Language), p.Program!);
        if (s.Fields.Contains("expiry") && s.Expiry is { } exp) yield return (Label("expiry", s.Language), exp.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void PhotoBox(IContainer box, IdCardPerson p, IdCardStyle s)
    {
        var frame = box.Border(2).BorderColor(s.Accent).Background(s.Secondary);
        if (p.Photo is { Length: > 0 })
        {
            frame.Image(p.Photo).FitArea();
        }
        else
        {
            frame.AlignCenter().AlignMiddle().Text(Initials(p.Name)).Bold().FontSize(28).FontColor(s.Primary);
        }
    }

    private static void Header(IContainer c, IdCardStyle s, bool onColour)
    {
        c.Row(row =>
        {
            row.Spacing(6);
            if (s.Logo is { Length: > 0 })
            {
                row.ConstantItem(26).Image(s.Logo).FitArea();
            }

            row.RelativeItem().AlignMiddle().Text(s.CentreName).Bold().FontSize(12).FontColor(onColour ? Colors.White : s.Primary);
        });
    }

    private static void Footer(IContainer c, IdCardPerson p, IdCardStyle s, float qrSize)
    {
        if (!s.Fields.Contains("barcode") || string.IsNullOrWhiteSpace(p.Barcode))
        {
            return;
        }

        c.Row(row =>
        {
            row.Spacing(6);
            row.ConstantItem(qrSize).Image(Qr(p.Barcode)).FitArea();
            row.RelativeItem().AlignMiddle().Text(p.Barcode).FontSize(8).FontColor(Colors.Grey.Darken2);
        });
    }

    private static void Portrait(IContainer root, IdCardPerson p, IdCardStyle s)
    {
        void Body(IContainer c) => c.Padding(14).Column(col =>
        {
            col.Spacing(6);
            if (s.Layout is not ("band" or "pill"))
            {
                col.Item().Element(h => Header(h, s, false));
            }

            if (s.Fields.Contains("photo"))
            {
                col.Item().AlignCenter().Height(120).Width(120).Element(b => PhotoBox(b, p, s));
            }

            if (s.Fields.Contains("name"))
            {
                col.Item().AlignCenter().Text(p.Name).Bold().FontSize(17).FontColor(s.Primary);
            }

            if (!string.IsNullOrWhiteSpace(p.Subtitle))
            {
                col.Item().AlignCenter().Text(p.Subtitle!).FontSize(11).FontColor(Colors.Grey.Darken2);
            }

            foreach (var (label, value) in Details(p, s))
            {
                col.Item().Row(r =>
                {
                    r.ConstantItem(80).Text(label).FontColor(Colors.Grey.Darken1).FontSize(9);
                    r.RelativeItem().Text(value).SemiBold();
                });
            }

            col.Item().ExtendVertical().AlignBottom().Element(f => Footer(f, p, s, 56));
        });

        switch (s.Layout)
        {
            case "band":
                root.Column(col =>
                {
                    col.Item().Background(s.Primary).Padding(10).Element(h => Header(h, s, true));
                    col.Item().Height(4).Background(s.Accent);
                    col.Item().ExtendVertical().Element(Body);
                });
                break;
            case "pill":
                root.Background(s.Primary).Padding(8).Column(col =>
                {
                    col.Item().PaddingVertical(4).PaddingHorizontal(6).Element(h => Header(h, s, true));
                    col.Item().ExtendVertical().Background(Colors.White).CornerRadius(10).Element(Body);
                });
                break;
            case "side":
                root.Row(row =>
                {
                    row.ConstantItem(26).Background(s.Primary);
                    row.RelativeItem().Element(Body);
                });
                break;
            case "frame":
                root.Padding(8).Border(3).BorderColor(s.Accent).Element(Body);
                break;
            default:
                root.Column(col =>
                {
                    col.Item().Height(6).Background(s.Accent);
                    col.Item().ExtendVertical().Element(Body);
                });
                break;
        }
    }

    private static void Landscape(IContainer root, IdCardPerson p, IdCardStyle s)
    {
        root.Column(col =>
        {
            col.Item().Background(s.Primary).PaddingVertical(4).PaddingHorizontal(8).Element(h => Header(h, s, true));
            col.Item().Height(2).Background(s.Accent);
            col.Item().ExtendVertical().Padding(8).Row(row =>
            {
                row.Spacing(8);
                if (s.Fields.Contains("photo"))
                {
                    row.ConstantItem(70).Height(86).Element(b => PhotoBox(b, p, s));
                }

                row.RelativeItem().Column(info =>
                {
                    info.Spacing(2);
                    if (s.Fields.Contains("name"))
                    {
                        info.Item().Text(p.Name).Bold().FontSize(11).FontColor(s.Primary);
                    }

                    if (!string.IsNullOrWhiteSpace(p.Subtitle))
                    {
                        info.Item().Text(p.Subtitle!).FontColor(Colors.Grey.Darken2);
                    }

                    foreach (var (label, value) in Details(p, s))
                    {
                        info.Item().Text(t =>
                        {
                            t.Span($"{label}: ").FontColor(Colors.Grey.Darken1);
                            t.Span(value).SemiBold();
                        });
                    }

                    info.Item().ExtendVertical().AlignBottom().Element(f => Footer(f, p, s, 34));
                });
            });
        });
    }
}
