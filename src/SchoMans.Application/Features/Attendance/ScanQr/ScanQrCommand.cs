namespace SchoMans.Application.Features.Attendance.ScanQr;

/// <param name="Token">The value of the QR code (the check-in address carries it in its <c>t</c> parameter).</param>
/// <param name="Latitude">The phone's location; required only when the school has the location check switched on.</param>
public sealed record ScanQrCommand(string Token, double? Latitude = null, double? Longitude = null);
