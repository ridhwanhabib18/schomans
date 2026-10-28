using FluentValidation;

namespace SchoMans.Application.Features.Attendance.ScanQr;

public sealed class ScanQrValidator : AbstractValidator<ScanQrCommand>
{
    public ScanQrValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Longitude).NotNull().When(x => x.Latitude is not null).WithMessage("Send both coordinates or neither.");
        RuleFor(x => x.Latitude).NotNull().When(x => x.Longitude is not null).WithMessage("Send both coordinates or neither.");
    }
}
