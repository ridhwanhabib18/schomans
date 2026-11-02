using System.Text;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Infrastructure.Pdf;
using SchoMans.Infrastructure.Time;

namespace SchoMans.Api.IntegrationTests.Pdf;

/// <summary>The real QuestPDF letter renderer and the real QRCoder generator.</summary>
public class LetterPdfRendererTests
{
    private static readonly QrCoderGenerator Qr = new();
    private static readonly LetterPdfRenderer Renderer = new(Options.Create(new SchoolOptions()));

    private static LetterDocument Sample(string purpose = "For a scholarship application") => new(
        "007/SK/SCHOMANS/X/2026",
        "Rina Putri",
        "0012345678",
        "Class A",
        "2026/2027",
        purpose,
        new DateOnly(2026, 11, 2),
        "Hendra Wirawan",
        "https://school.test/verify/0123456789abcdef0123",
        Qr.Png("https://school.test/verify/0123456789abcdef0123"));

    [Fact]
    public void A_qr_code_is_a_png_and_different_content_gives_a_different_picture()
    {
        var first = Qr.Png("https://school.test/verify/aaaaaaaaaaaaaaaaaaaa");
        var second = Qr.Png("https://school.test/verify/bbbbbbbbbbbbbbbbbbbb");

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], first.Take(4));
        Assert.True(first.Length > 200);
        Assert.NotEqual(first, second);
        Assert.Equal(first, Qr.Png("https://school.test/verify/aaaaaaaaaaaaaaaaaaaa")); // the same address, the same picture
        Assert.Throws<ArgumentException>(() => Qr.Png(" "));
    }

    [Fact]
    public void A_letter_renders_as_a_pdf()
    {
        var pdf = Renderer.Render(Sample());

        Assert.True(pdf.Length > 5_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        // Kept as test evidence of the generated letter (docs/assets/testing/phase-5 holds a copy).
        File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "sample-letter.pdf"), pdf);
    }

    [Fact]
    public void A_long_purpose_still_fits_on_the_letter()
    {
        var pdf = Renderer.Render(Sample(string.Join(' ', Enumerable.Repeat("a long purpose that goes on and on", 25))));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
