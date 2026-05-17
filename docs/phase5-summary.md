# Phase 5 — Webhooks

**Status:** Complete  
**Date completed:** 2026-05-17

---

## What Was Built

### Webhook subscriptions (per-establishment)
Developers can subscribe to a specific establishment's score changes. When the daily XML sync detects that `LatestScore` changed for an establishment, a Hangfire job is enqueued to POST a signed payload to the subscriber's callback URL.

### New endpoints (all require `X-Api-Key` header)
| Method | Path | Description |
|---|---|---|
| `POST` | `/v1/webhooks` | Subscribe: body `{ "establishmentId": int, "callbackUrl": "https://..." }` |
| `DELETE` | `/v1/webhooks/{id}` | Unsubscribe (must own the subscription) |
| `GET` | `/v1/webhooks` | List own subscriptions |

### HMAC-SHA256 payload signing
Each subscription has a `SecretKey` (32 random bytes, hex-encoded). The secret is returned **only on creation** — never again. Every delivery includes the header:
```
X-Smiley-Signature: sha256=<hex>
X-Smiley-Event: smiley_score_changed
```
Subscribers verify by computing `HMACSHA256(secret, body)` and comparing to the header.

### Payload shape
```json
{
  "event": "smiley_score_changed",
  "occurredAt": "2026-05-17T10:30:00Z",
  "establishment": {
    "id": 123,
    "navnelbnr": 1001234,
    "cvr": "12345678",
    "name": "Café Nørrebro"
  },
  "currentScore": 1,
  "previousScore": 2
}
```

### Hangfire job queue (SQL Server)
- Package: `Hangfire.AspNetCore` + `Hangfire.SqlServer` (v1.8.23)
- Uses the same Azure SQL / LocalDB connection string — no extra Azure service needed
- Hangfire tables (`HangFire.*`) are created automatically on first startup
- Default retry policy: 10 attempts, exponential backoff
- Dashboard: `/hangfire` (dev only, not exposed in production)

### Score-change detection in `EstablishmentSyncService`
The establishments `MERGE` statement now uses an extended `OUTPUT` clause that captures `DELETED.LatestScore` (before) and `INSERTED.LatestScore` (after) for each row. A second result set returns only the rows where the score actually changed. `SyncAsync` now returns `IReadOnlyList<ScoreChange>` instead of `Task`.

---

## Files Changed

| File | Change |
|---|---|
| `src/SmileyApi.Core/Models/WebhookSubscription.cs` | Added `SecretKey` property |
| `src/SmileyApi.Infrastructure/Data/SmileyDbContext.cs` | Added `HasMaxLength(64)` for `SecretKey` |
| `src/SmileyApi.Infrastructure/Services/EstablishmentSyncService.cs` | Extended MERGE OUTPUT, added `ScoreChange` record, changed return type |
| `src/SmileyApi.Infrastructure/Services/WebhookService.cs` | **New** — subscribe, unsubscribe, list, enqueue deliveries |
| `src/SmileyApi.Infrastructure/Jobs/WebhookDeliveryJob.cs` | **New** — Hangfire job for HMAC-signed HTTP delivery |
| `src/SmileyApi.Api/Endpoints/WebhookEndpoints.cs` | **New** — 3 webhook endpoints |
| `src/SmileyApi.Api/Workers/XmlSyncWorker.cs` | Passes score changes to `WebhookService.EnqueueDeliveriesAsync` |
| `src/SmileyApi.Api/Program.cs` | Hangfire DI, `WebhookService`, `WebhookDeliveryJob`, dashboard, `MapWebhookEndpoints` |
| `src/SmileyApi.Infrastructure/Data/Migrations/` | Migration `AddWebhookSubscriptionsAndSecret` |
| `src/SmileyApi.Api/wwwroot/index.html` | Removed "Coming soon" badge from Webhooks feature card |
| `CLAUDE.md` | Removed "Webhooks and Hangfire" from Do Not Build Yet list |

---

## Testing

To test locally:
1. Start the API: `dotnet run --project src/SmileyApi.Api`
2. Generate a dev API key: `POST /admin/keys` `{"email":"test@test.com","tier":"pro"}`
3. Get an establishment ID from the DB (any row in `Establishments`)
4. Subscribe: `POST /v1/webhooks` with `X-Api-Key` header, body `{"establishmentId": <id>, "callbackUrl": "https://webhook.site/<your-id>"}`
5. Save the returned `secret`
6. Force a sync by restarting the app
7. Check the Hangfire dashboard at `/hangfire` for job status
8. Verify webhook.site received the payload and validate the `X-Smiley-Signature` header
