using QRCoder;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Pdf;

/// <summary>Draws a QR code as a PNG with QRCoder (MIT).</summary>
public sealed class QrCoderGenerator : IQrCodeGenerator
{
    /// <summary>Pixels per QR module; 10 keeps the code sharp when it is printed at 2 cm.</summary>
    private const int PixelsPerModule = 10;

    public byte[] Png(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
    }
}
