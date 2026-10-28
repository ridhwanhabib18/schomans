namespace SchoMans.Application.Features.Students.GetStudentImportTemplate;

public sealed record GetStudentImportTemplateQuery;

public sealed record StudentImportTemplate(byte[] Content, string FileName, string ContentType);
