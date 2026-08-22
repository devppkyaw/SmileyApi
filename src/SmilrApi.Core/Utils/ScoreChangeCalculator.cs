using SmilrApi.Core.Models;

namespace SmilrApi.Core.Utils;

/// <summary>
/// Derives an establishment's current score transition directly from its Inspections history —
/// no separate change log is persisted. Inspections is an accumulating, insert-only table (see
/// EstablishmentSyncService's MERGE), so this works retroactively over the full recorded history,
/// not just changes detected going forward from some start date.
/// </summary>
public static class ScoreChangeCalculator
{
    public readonly record struct Change(int PreviousScore, int NewScore, DateOnly ChangeDate);

    /// <summary>Walks an establishment's inspection history, newest first, and returns its current,
    /// most recent score transition — the first point (scanning backward from the newest inspection)
    /// where a score differs from the one immediately before it. Returns null if the establishment has
    /// only ever recorded one score, or every recorded score to date has been identical (no transition
    /// has ever occurred). A same-score re-inspection after a real transition does NOT erase that
    /// transition — e.g. history newest-first [Aug20: score 1, Aug10: score 1, Jul1: score 2] still
    /// reports Jul1-&gt;Aug10 (2-&gt;1) as the current change, not "no change", because Aug20 didn't move
    /// anything. This is why the walk can't stop at just the two newest rows.</summary>
    public static Change? LatestChange(IEnumerable<Inspection> historyNewestFirst)
    {
        using var e = historyNewestFirst.GetEnumerator();
        if (!e.MoveNext()) return null;
        var newer = e.Current;
        while (e.MoveNext())
        {
            var older = e.Current;
            if (newer.SmileyScore != older.SmileyScore)
                return new Change(older.SmileyScore, newer.SmileyScore, newer.InspectedOn);
            newer = older;
        }
        return null;
    }
}
