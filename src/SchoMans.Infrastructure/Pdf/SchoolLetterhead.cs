using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SchoMans.Infrastructure.Time;

namespace SchoMans.Infrastructure.Pdf;

/// <summary>The letterhead of the school (logo, name, address, contact and a rule), shared by the report card and the letters.</summary>
internal static class SchoolLetterhead
{
    public const string Accent = "#1e3a8a";
    public const string Ink = "#111827";
    public const string Muted = "#6b7280";

    private static readonly string LogoSvg = LoadLogo();

    public static void Compose(IContainer container, SchoolOptions school) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(56).Height(56).Svg(LogoSvg);
                row.RelativeItem().PaddingLeft(12).Column(text =>
                {
                    text.Item().Text(school.Name).FontSize(16).Bold().FontColor(Accent);
                    text.Item().Text(school.Address).FontSize(9).FontColor(Muted);
                    text.Item().Text(school.Contact).FontSize(9).FontColor(Muted);
                });
            });
            column.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Accent);
        });

    private static string LoadLogo()
    {
        var assembly = typeof(SchoolLetterhead).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("school-logo.svg", StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        return reader.ReadToEnd();
    }
}
