namespace SmilrApi.Core.Utils;

/// <summary>
/// Helpers for the Establishment.Pixibranche field. It's a mostly-controlled vocabulary in the source
/// data (~26 values), but two of those are administrative "not yet classified" markers rather than
/// real categories — they must never surface as a browsable /find category. The source has drifted
/// wording before without warning (e.g. "detail-branche" -> "detailbranche" between syncs, confirmed
/// live 2026-09-16 — see project_smilr_feed_migration_2026-09 memory), so placeholder detection matches
/// on the stable "endnu ikke tildelt" ("not yet assigned") suffix rather than the full exact string.
/// </summary>
public static class PixibrancheCategories
{
    // The stable part of the two "not yet classified" marker values — kept as a plain constant
    // (rather than only inside IsPlaceholder below) so it can also be inlined directly into EF Core
    // LINQ queries that need SQL translation, where a call to IsPlaceholder itself won't translate.
    public const string PlaceholderSuffix = "endnu ikke tildelt";

    // Kept for reference/back-compat call sites, not used by IsPlaceholder itself anymore.
    public static readonly string[] Placeholders =
    [
        "Virksomheder, detail-branche endnu ikke tildelt",
        "Virksomheder, engros-branche endnu ikke tildelt"
    ];

    public static bool IsPlaceholder(string? pixibranche) =>
        pixibranche is not null &&
        pixibranche.TrimEnd().EndsWith(PlaceholderSuffix, StringComparison.OrdinalIgnoreCase);
}
