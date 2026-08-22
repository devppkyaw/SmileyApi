using SmilrApi.Core.Models;
using SmilrApi.Core.Utils;

namespace SmilrApi.Api.Rendering;

/// <summary>
/// Single source of truth for building and canonicalizing /find URLs. Slugs (area and business-name)
/// are computed on the fly from Establishment.City/Name — never persisted — so both link generation
/// (here, and in FindPageRenderer) and canonical-redirect matching (in FindEndpoints) must go through
/// this class to stay in agreement.
/// </summary>
public static class FindUrlBuilder
{
    public static string AreaSlug(string city) => Slugifier.Slugify(city);

    public static string BusinessSlug(string name) => Slugifier.Slugify(name, fallback: "virksomhed");

    /// <summary>Area hub page path, e.g. "/find/kobenhavn/". Always has a trailing slash.</summary>
    public static string HubPath(string city) => $"/find/{AreaSlug(city)}/";

    /// <summary>
    /// Canonical establishment detail path. Establishments with a City get the two-segment
    /// "/find/{area-slug}/{business-slug}-{navnelbnr}" form; establishments with no City (rare, but the
    /// raw source data isn't guaranteed to have one) fall back to the one-segment
    /// "/find/{business-slug}-{navnelbnr}" form, since there's no area to nest them under.
    /// </summary>
    public static string DetailPath(string name, string? city, int navnelbnr)
    {
        var segment = $"{BusinessSlug(name)}-{navnelbnr}";
        return string.IsNullOrWhiteSpace(city) ? $"/find/{segment}" : $"/find/{AreaSlug(city)}/{segment}";
    }

    public static string DetailPath(Establishment est) => DetailPath(est.Name, est.City, est.Navnelbnr);

    public static string CategorySlug(string pixibranche) => Slugifier.Slugify(pixibranche);

    /// <summary>Category hub page path, e.g. "/find/kobenhavn/bagere-og-bagerafdelinger". No trailing slash
    /// (unlike HubPath) — matches the existing detail-page path style.</summary>
    public static string CategoryHubPath(string city, string pixibranche) =>
        $"/find/{AreaSlug(city)}/{CategorySlug(pixibranche)}";

    /// <summary>"Recently inspected" hub page path, e.g. "/find/kobenhavn/recently-inspected".
    /// "recently-inspected" is a reserved segment (see FindEndpoints.MapFindEndpoints) — never a real
    /// category-slug value, since Pixibranche is a controlled vocabulary checked separately.</summary>
    public static string RecentlyInspectedPath(string city) => $"/find/{AreaSlug(city)}/recently-inspected";

    /// <summary>Score-change feed page path, e.g. "/find/kobenhavn/changes". "changes" is a reserved
    /// segment (see FindEndpoints.MapFindEndpoints), same reasoning as RecentlyInspectedPath.</summary>
    public static string ChangesPath(string city) => $"/find/{AreaSlug(city)}/changes";
}
