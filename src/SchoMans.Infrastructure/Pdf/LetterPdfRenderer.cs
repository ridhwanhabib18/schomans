using System.Globalization;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Infrastructure.Time;

namespace SchoMans.Infrastructure.Pdf;

/// <summary>Draws the certificate of enrollment with QuestPDF: letterhead, number, the statement, the signature and a QR code to check the letter.</summary>
public sealed class LetterPdfRenderer : ILetterPdf
{
    private readonly SchoolOptions _school;

    static LetterPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public LetterPdfRenderer(IOptions<SchoolOptions> options) => _school = options.Value;

    public byte[] Render(LetterDocument document) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(48);
            page.DefaultTextStyle(text => text.FontSize(11).FontColor(SchoolLetterhead.Ink).LineHeight(1.4f));

            page.Header().Element(header => SchoolLetterhead.Compose(header, _school));
            page.Content().PaddingVertical(24).Element(content => Body(content, document));
            page.Footer().Row(row =>
            {
                row.ConstantItem(80).Height(80).Image(document.QrPng);
                row.RelativeItem().PaddingLeft(12).AlignMiddle().Column(text =>
                {
                    text.Item().Text("Check that this letter is real").FontSize(9).Bold();
                    text.Item().Text("Scan the code or open the address below. It shows the number, the name and the date of this letter.").FontSize(8).FontColor(SchoolLetterhead.Muted);
                    text.Item().Text(document.VerificationUrl).FontSize(8).FontColor(SchoolLetterhead.Accent);
                });
            });
        })).GeneratePdf();

    private static void Body(IContainer container, LetterDocument document) =>
        container.Column(column =>
        {
            column.Spacing(14);

            column.Item().AlignCenter().Text("CERTIFICATE OF ENROLLMENT").FontSize(15).Bold();
            column.Item().AlignCenter().Text($"Number: {document.LetterNumber}").FontColor(SchoolLetterhead.Muted);

            column.Item().Text("This is to certify that the student below is registered as an active student of this school:");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(110);
                    columns.RelativeColumn();
                });

                Row(table, "Name", document.StudentName);
                Row(table, "NISN", document.Nisn);
                Row(table, "Class", document.ClassroomName);
                Row(table, "Academic year", document.AcademicYearName);
            });

            column.Item().Text($"The letter is issued at the request of the parent or the student, for: {document.Purpose}");
            column.Item().Text("It is issued for the purpose named above and is valid only with the code on this page.");

            column.Item().PaddingTop(28).AlignRight().Width(200).Column(sign =>
            {
                sign.Item().AlignCenter().Text(document.IssuedOn.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));
                sign.Item().Height(52);
                sign.Item().LineHorizontal(0.75f).LineColor(SchoolLetterhead.Ink);
                sign.Item().AlignCenter().Text(document.SignedByName).Bold();
            });
        });

    private static void Row(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(2).Text(label).FontColor(SchoolLetterhead.Muted);
        table.Cell().PaddingVertical(2).Text(value).Bold();
    }
}
