using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SmilrApi.Core.Utils;

/// <summary>
/// Turns free text (establishment names, city names) into URL-safe slugs for the /find directory.
/// Slugs are computed on the fly from Name/City at request time — never persisted — so this is the
/// single place both link-generation and canonical-URL matching must agree on.
/// </summary>
public static class Slugifier
{
    private static readonly Regex NonAlphanumericRun = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    public static string Slugify(string? input, string fallback = "x")
    {
        if (string.IsNullOrWhiteSpace(input))
            return fallback;

        // Danish letters are standalone Unicode code points, not decomposable base+mark sequences,
        // so Unicode normalization alone won't fold them — replace explicitly, before lower-casing.
        var text = input
            .Replace("æ", "ae").Replace("Æ", "ae")
            .Replace("ø", "oe").Replace("Ø", "oe")
            .Replace("å", "aa").Replace("Å", "aa")
            .ToLowerInvariant();

        // Fold any other accented Latin letters (é, ü, ö, ñ, ...) down to their base ASCII letter.
        text = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        text = sb.ToString().Normalize(NormalizationForm.FormC);

        text = NonAlphanumericRun.Replace(text, "-").Trim('-');

        return text.Length == 0 ? fallback : text;
    }
}
