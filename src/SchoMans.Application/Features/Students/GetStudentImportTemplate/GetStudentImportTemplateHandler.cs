using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Students.GetStudentImportTemplate;

public sealed class GetStudentImportTemplateHandler(IStudentTemplateWriter writer)
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public Task<Result<StudentImportTemplate>> Handle(GetStudentImportTemplateQuery query, CancellationToken ct)
    {
        Result<StudentImportTemplate> result = new StudentImportTemplate(writer.CreateTemplate(), "students-import-template.xlsx", XlsxContentType);
        return Task.FromResult(result);
    }
}
