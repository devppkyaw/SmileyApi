using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmilrApi.Core.Interfaces;
using SmilrApi.Core.Models;
using SmilrApi.Core.Utils;
using SmilrApi.Infrastructure.Data;

namespace SmilrApi.Infrastructure.Services;

/// <summary>
/// Orchestrates FeedHealthEvaluator against the persisted FeedHealthSnapshot/FeedHealthAnomaly
/// state, emails system@smilrhq.dk (via IEmailService, same as SendSystemScoreDigestAsync) for
/// whatever FeedHealthEvaluator decides needs one, then persists the updated state. Called once
/// per XmlSyncWorker run, right after FodevareXmlParser.ParseAsync succeeds.
/// </summary>
public class FeedHealthCheckService(
    SmilrDbContext db,
    IEmailService emailService,
    IOptions<FeedHealthOptions> options,
    ILogger<FeedHealthCheckService> logger)
{
    public async Task CheckAndAlertAsync(FeedHealthMetrics metrics, CancellationToken ct)
    {
        var opts = options.Value;
        if (!opts.Enabled)
            return;

        var snapshot = await db.FeedHealthSnapshots.SingleOrDefaultAsync(ct);
        var priorAnomalies = await db.FeedHealthAnomalies.ToListAsync(ct);
        var now = DateTime.UtcNow;

        var evaluation = FeedHealthEvaluator.Evaluate(metrics, snapshot, priorAnomalies, opts, now);

        foreach (var decision in evaluation.Decisions)
        {
            try
            {
                await emailService.SendFeedHealthAlertAsync(
                    decision.Kind, decision.FieldName, decision.Status, decision.Summary, decision.FirstDetectedAt, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send feed health alert ({Kind}/{Field}, {Status}).",
                    decision.Kind, decision.FieldName, decision.Status);
            }
        }

        await PersistAsync(snapshot, priorAnomalies, evaluation, ct);
    }

    private async Task PersistAsync(
        FeedHealthSnapshot? existingSnapshot,
        IReadOnlyList<FeedHealthAnomaly> existingAnomalies,
        FeedHealthEvaluation evaluation,
        CancellationToken ct)
    {
        if (existingSnapshot is null)
            db.FeedHealthSnapshots.Add(evaluation.UpdatedSnapshot);
        else
            db.Entry(existingSnapshot).CurrentValues.SetValues(evaluation.UpdatedSnapshot);

        var existingById = existingAnomalies.Where(a => a.Id != 0).ToDictionary(a => a.Id);
        foreach (var anomaly in evaluation.UpdatedAnomalies)
        {
            if (anomaly.Id != 0 && existingById.TryGetValue(anomaly.Id, out var tracked))
                db.Entry(tracked).CurrentValues.SetValues(anomaly);
            else
                db.FeedHealthAnomalies.Add(anomaly);
        }

        await db.SaveChangesAsync(ct);
    }
}
