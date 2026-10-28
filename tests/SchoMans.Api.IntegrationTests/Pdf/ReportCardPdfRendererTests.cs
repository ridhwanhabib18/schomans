using System.Text;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Infrastructure.Pdf;
using SchoMans.Infrastructure.Time;

namespace SchoMans.Api.IntegrationTests.Pdf;

/// <summary>The real QuestPDF renderer: it must produce a PDF for a full report card and for one without grades.</summary>
public class ReportCardPdfRendererTests
{
    private static readonly ReportCardPdfRenderer Renderer = new(Options.Create(new SchoolOptions()));

    public static ReportCardDocument Sample(IReadOnlyList<ReportCardLine>? lines = null) => new(
        "Rina Putri",
        "0012345678",
        "Class A",
        "Term 1",
        "2026/2027",
        "Budi Santoso",
        new DateOnly(2026, 12, 18),
        new AttendanceSummary(88, 2, 1, 1),
        lines ??
        [
            new ReportCardLine("Mathematics", 75, 86.5m, "B", true, "Masters fractions and solves word problems with confidence."),
            new ReportCardLine("English", 75, 68m, "D", false, "Reads well; needs more practice in writing."),
            new ReportCardLine("Arts and Culture", 70, null, null, null, null),
        ]);

    [Fact]
    public void A_report_card_renders_as_a_pdf()
    {
        var pdf = Renderer.Render(Sample());

        Assert.True(pdf.Length > 5_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        // Kept as test evidence of the generated report card (docs/assets/testing/phase-4 holds a copy).
        File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "sample-report-card.pdf"), pdf);
    }

    [Fact]
    public void A_report_card_without_grades_still_renders()
    {
        var pdf = Renderer.Render(Sample([]));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
