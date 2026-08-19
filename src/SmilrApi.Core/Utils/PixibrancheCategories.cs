namespace SmilrApi.Core.Utils;

/// <summary>
/// Helpers for the Establishment.Pixibranche field. It's a controlled vocabulary in the source data
/// (~26 fixed values), but two of those values are administrative "not yet classified" markers rather
/// than real categories — they must never surface as a browsable /find category.
/// </summary>
public static class PixibrancheCategories
{
    public static readonly string[] Placeholders =
    [
        "Virksomheder, detail-branche endnu ikke tildelt",
        "Virksomheder, engros-branche endnu ikke tildelt"
    ];

    public static bool IsPlaceholder(string? pixibranche) =>
        pixibranche is not null && Placeholders.Contains(pixibranche);
}
