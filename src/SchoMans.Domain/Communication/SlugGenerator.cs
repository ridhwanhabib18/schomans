using System.Globalization;
using System.Text;

namespace SchoMans.Domain.Communication;

/// <summary>Turns a title into a URL slug: lower case ASCII words joined by dashes, for example "Open House 2027" becomes "open-house-2027".</summary>
public static class SlugGenerator
{
    public const int MaxLength = 80;

    public static string Slugify(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var decomposed = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingDash = false;

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue; // drop the accent of "é", keep the "e"
            }

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(c);
                pendingDash = false;
            }
            else if (c is >= 'A' and <= 'Z')
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(c));
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }

        var slug = builder.ToString();
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        // A title made only of symbols still needs a usable address.
        return slug.Length == 0 ? "article" : slug;
    }

    /// <summary>The slug with a numeric suffix for the second, third, ... article with the same title.</summary>
    public static string WithSuffix(string slug, int number) => number <= 1 ? slug : $"{slug}-{number}";
}
