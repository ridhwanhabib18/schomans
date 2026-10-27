using FluentValidation;

namespace SchoMans.Application.Features.Students.ImportStudents;

public sealed class ImportStudentsValidator : AbstractValidator<ImportStudentsCommand>
{
    public ImportStudentsValidator()
    {
        RuleFor(x => x.Length)
            .InclusiveBetween(1, ImportStudentsCommand.MaxBytes)
            .WithMessage("The file must not be empty and at most 5 MB.");
        RuleFor(x => x.FileName)
            .Must(name => name is not null && name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Upload an .xlsx file. Download the template to see the format.");
    }
}
