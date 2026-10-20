using NMS.Application.Tools;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NMS.Infrastructure.Services;

/// <summary>Draws one certificate per page on A4 landscape.</summary>
public sealed class QuestCertificateRenderer : ICertificateRenderer
{
    static QuestCertificateRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(IReadOnlyList<CertificateItem> items, CertificateStyle style) =>
        Document.Create(document =>
        {
            foreach (var item in items)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(0);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(13));
                    if (style.Language == "ar")
                    {
                        page.ContentFromRightToLeft();
                    }

                    page.Content().Element(c => Frame(c, item, style));
                });
            }
        }).GeneratePdf();

    private static void Frame(IContainer root, CertificateItem item, CertificateStyle s)
    {
        switch (s.Layout)
        {
            case "border":
                root.Padding(18).Border(4).BorderColor(s.Accent).Padding(6).Border(1.5f).BorderColor(s.Primary).Element(c => Body(c, item, s));
                break;
            case "ribbon":
                root.Column(col =>
                {
                    col.Item().Height(34).Background(s.Primary);
                    col.Item().Height(6).Background(s.Accent);
                    col.Item().ExtendVertical().Element(c => Body(c, item, s));
                    col.Item().Height(6).Background(s.Accent);
                    col.Item().Height(34).Background(s.Primary);
                });
                break;
            case "side":
                root.Row(row =>
                {
                    row.ConstantItem(46).Background(s.Primary);
                    row.ConstantItem(8).Background(s.Accent);
                    row.RelativeItem().Element(c => Body(c, item, s));
                });
                break;
            case "playful":
                root.Background(Colors.White).Padding(14).Border(10).BorderColor(s.Accent).CornerRadius(26).Padding(6).Border(3).BorderColor(s.Primary).CornerRadius(20).Element(c => Body(c, item, s));
                break;
            case "modern":
                root.Column(col =>
                {
                    col.Item().Background(s.Primary).Padding(14).Element(h => Header(h, s, true));
                    col.Item().Height(5).Background(s.Accent);
                    col.Item().ExtendVertical().Element(c => Body(c, item, s, showHeader: false));
                });
                break;
            default:
                root.Padding(26).Border(2).BorderColor(s.Primary).Padding(10).Border(0.75f).BorderColor(s.Accent).Element(c => Body(c, item, s));
                break;
        }
    }

    private static void Header(IContainer c, CertificateStyle s, bool onColour)
    {
        c.Row(row =>
        {
            row.Spacing(10);
            if (s.Logo is { Length: > 0 })
            {
                row.ConstantItem(46).Image(s.Logo).FitArea();
            }

            row.RelativeItem().AlignMiddle().Text(s.CentreName).Bold().FontSize(16).FontColor(onColour ? Colors.White : s.Primary);
        });
    }

    private static void Body(IContainer c, CertificateItem item, CertificateStyle s, bool showHeader = true)
    {
        c.PaddingHorizontal(46).PaddingVertical(22).Column(col =>
        {
            col.Spacing(10);
            if (showHeader)
            {
                col.Item().AlignCenter().Element(h => Header(h, s, false));
            }

            col.Item().PaddingTop(8).AlignCenter().Text(item.Title).Bold().FontSize(34).FontColor(s.Primary);
            col.Item().AlignCenter().Text(s.Language == "ar" ? "تُمنح إلى" : "Presented to").FontSize(14).FontColor(Colors.Grey.Darken1);
            col.Item().AlignCenter().BorderBottom(1.5f).BorderColor(s.Accent).PaddingHorizontal(30).PaddingBottom(4).Text(item.Recipient).Bold().FontSize(30).FontColor(Colors.Black);
            col.Item().PaddingHorizontal(40).AlignCenter().Text(item.Body).FontSize(15).LineHeight(1.4f).AlignCenter();
            col.Item().ExtendVertical().AlignBottom().Row(row =>
            {
                row.RelativeItem().AlignBottom().Column(d =>
                {
                    d.Item().Text(item.DateText).SemiBold();
                    d.Item().Text($"{(s.Language == "ar" ? "الرقم" : "No.")} {item.Number}").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
                foreach (var sig in s.Signatures)
                {
                    row.RelativeItem().AlignBottom().PaddingHorizontal(8).Column(g =>
                    {
                        g.Item().PaddingTop(26).BorderTop(1).BorderColor(Colors.Grey.Darken2).PaddingTop(3).AlignCenter().Text(sig.Name).SemiBold().FontSize(11);
                        if (!string.IsNullOrWhiteSpace(sig.Title))
                        {
                            g.Item().AlignCenter().Text(sig.Title!).FontSize(9).FontColor(Colors.Grey.Darken1);
                        }
                    });
                }
            });
        });
    }
}
