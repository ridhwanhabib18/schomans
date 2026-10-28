using System.Globalization;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Infrastructure.Time;

namespace SchoMans.Infrastructure.Pdf;

/// <summary>Draws the term report card with QuestPDF (Community License): letterhead, student data, grades, attendance and the signature of the homeroom teacher.</summary>
public sealed class ReportCardPdfRenderer : IReportCardPdf
{
    private const string Ink = "#111827";
    private const string Muted = "#6b7280";
    private const string Accent = "#1e3a8a";
    private const string Line = "#d1d5db";
    private const string Warning = "#b91c1c";

    private static readonly string LogoSvg = LoadLogo();

    private readonly SchoolOptions _school;

    static ReportCardPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public ReportCardPdfRenderer(IOptions<SchoolOptions> options) => _school = options.Value;

    public byte[] Render(ReportCardDocument document) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(text => text.FontSize(10).FontColor(Ink));

            page.Header().Element(header => Letterhead(header, document));
            page.Content().PaddingVertical(12).Element(content => Body(content, document));
            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf();

    private void Letterhead(IContainer container, ReportCardDocument document) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(56).Height(56).Svg(LogoSvg);
                row.RelativeItem().PaddingLeft(12).Column(text =>
                {
                    text.Item().Text(_school.Name).FontSize(16).Bold().FontColor(Accent);
                    text.Item().Text(_school.Address).FontSize(9).FontColor(Muted);
                    text.Item().Text(_school.Contact).FontSize(9).FontColor(Muted);
                });
            });
            column.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Accent);
        });

    private static void Body(IContainer container, ReportCardDocument document) =>
        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().AlignCenter().Text("TERM REPORT CARD").FontSize(14).Bold();

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(90);
                    columns.RelativeColumn();
                    columns.ConstantColumn(90);
                    columns.RelativeColumn();
                });

                InfoRow(table, "Name", document.StudentName, "Term", document.TermName);
                InfoRow(table, "NISN", document.Nisn, "Academic year", document.AcademicYearName);
                InfoRow(table, "Class", document.ClassroomName, "Issued on", document.IssuedOn.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(44);
                    columns.ConstantColumn(44);
                    columns.ConstantColumn(40);
                    columns.RelativeColumn(4);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Subject");
                    HeaderCell(header.Cell(), "Min.");
                    HeaderCell(header.Cell(), "Grade");
                    HeaderCell(header.Cell(), "Band");
                    HeaderCell(header.Cell(), "Achievement");
                });

                foreach (var line in document.Lines)
                {
                    var below = line.IsPassing == false;
                    BodyCell(table.Cell(), line.Subject);
                    BodyCell(table.Cell(), Format(line.MinimumPassingScore));
                    BodyCell(table.Cell(), line.FinalGrade is null ? "-" : Format(line.FinalGrade.Value), below ? Warning : Ink, bold: true);
                    BodyCell(table.Cell(), line.Band ?? "-");
                    BodyCell(table.Cell(), line.Description ?? string.Empty);
                }

                if (document.Lines.Count == 0)
                {
                    table.Cell().ColumnSpan(5).Padding(8).Text("No grades have been recorded for this term.").FontColor(Muted);
                }
            });

            column.Item().Text(text =>
            {
                text.Span("Attendance: ").Bold();
                text.Span($"present {document.Attendance.Present}, excused {document.Attendance.Excused}, sick {document.Attendance.Sick}, absent {document.Attendance.Absent}");
            });

            column.Item().Text("A grade in red is below the minimum passing score of the subject.").FontSize(8).FontColor(Muted);

            column.Item().PaddingTop(24).AlignRight().Width(180).Column(sign =>
            {
                sign.Item().AlignCenter().Text("Homeroom teacher");
                sign.Item().Height(48);
                sign.Item().LineHorizontal(0.75f).LineColor(Ink);
                sign.Item().AlignCenter().Text(document.HomeroomTeacherName).Bold();
            });
        });

    private static void InfoRow(TableDescriptor table, string leftLabel, string leftValue, string rightLabel, string rightValue)
    {
        table.Cell().PaddingVertical(2).Text(leftLabel).FontColor(Muted);
        table.Cell().PaddingVertical(2).Text(leftValue).Bold();
        table.Cell().PaddingVertical(2).Text(rightLabel).FontColor(Muted);
        table.Cell().PaddingVertical(2).Text(rightValue).Bold();
    }

    private static void HeaderCell(IContainer cell, string label) =>
        cell.Background(Accent).Padding(5).Text(label).FontColor(Colors.White).Bold().FontSize(9);

    private static void BodyCell(IContainer cell, string value, string color = Ink, bool bold = false)
    {
        var text = cell.BorderBottom(0.5f).BorderColor(Line).Padding(5).Text(value).FontColor(color);
        if (bold)
        {
            text.Bold();
        }
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string LoadLogo()
    {
        var assembly = typeof(ReportCardPdfRenderer).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("school-logo.svg", StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        return reader.ReadToEnd();
    }
}
