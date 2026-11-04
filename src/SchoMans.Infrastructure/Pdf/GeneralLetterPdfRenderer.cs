using System.Globalization;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Infrastructure.Time;

namespace SchoMans.Infrastructure.Pdf;

/// <summary>Draws a general letter with QuestPDF: letterhead, number, recipient, subject, the approved paragraphs and the signature.</summary>
public sealed class GeneralLetterPdfRenderer : IGeneralLetterPdf
{
    private readonly SchoolOptions _school;

    static GeneralLetterPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public GeneralLetterPdfRenderer(IOptions<SchoolOptions> options) => _school = options.Value;

    public byte[] Render(GeneralLetterDocument document) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(48);
            page.DefaultTextStyle(text => text.FontSize(11).FontColor(SchoolLetterhead.Ink).LineHeight(1.4f));

            page.Header().Element(header => SchoolLetterhead.Compose(header, _school));
            page.Content().PaddingVertical(24).Element(content => Body(content, document));
        })).GeneratePdf();

    private static void Body(IContainer container, GeneralLetterDocument document) =>
        container.Column(column =>
        {
            column.Spacing(12);

            column.Item().AlignCenter().Text(document.Heading.ToUpperInvariant()).FontSize(15).Bold();
            column.Item().AlignCenter().Text($"Number: {document.LetterNumber}").FontColor(SchoolLetterhead.Muted);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(70);
                    columns.RelativeColumn();
                });

                table.Cell().PaddingVertical(2).Text("To").FontColor(SchoolLetterhead.Muted);
                table.Cell().PaddingVertical(2).Text(document.Recipient).Bold();
                table.Cell().PaddingVertical(2).Text("Subject").FontColor(SchoolLetterhead.Muted);
                table.Cell().PaddingVertical(2).Text(document.Subject).Bold();
            });

            foreach (var paragraph in document.Paragraphs)
            {
                column.Item().Text(paragraph);
            }

            column.Item().PaddingTop(28).AlignRight().Width(200).Column(sign =>
            {
                sign.Item().AlignCenter().Text(document.IssuedOn.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));
                sign.Item().Height(52);
                sign.Item().LineHorizontal(0.75f).LineColor(SchoolLetterhead.Ink);
                sign.Item().AlignCenter().Text(document.SignedByName).Bold();
            });
        });
}
