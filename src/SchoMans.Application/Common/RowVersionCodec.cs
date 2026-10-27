namespace SchoMans.Application.Common;

/// <summary>Row versions travel to the client as base64 text and come back unchanged with an update.</summary>
public static class RowVersionCodec
{
    public static string Encode(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static bool TryDecode(string? text, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(text);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
