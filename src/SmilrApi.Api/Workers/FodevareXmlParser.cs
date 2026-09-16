using System.Xml;
using System.Xml.Linq;

namespace SmilrApi.Api.Workers;

public class FodevareXmlParser(IHttpClientFactory httpClientFactory, ILogger<FodevareXmlParser> logger)
{
    // Moved here from foedevarestyrelsen.dk (that feed froze 2026-09-08 and stopped being
    // regenerated) — see https://www.findsmiley.dk/om-smiley/statistik-og-data/hent-smileydata.
    // Schema changed along with the move: PascalCase tag names, a renamed/revalued category field
    // (Pixibranche -> Smileybranche), MM/dd/yyyy dates, and no Geo_Lat/Geo_Lng at all.
    private const string XmlUrl = "https://pub.fvst.dk/publikationer/Smileydata.xml";

    public async Task<FodevareFeedResult> ParseAsync(CancellationToken ct)
    {
        logger.LogInformation("Downloading Smiley XML from Fødevarestyrelsen...");

        var client = httpClientFactory.CreateClient("fodevarestyrelsen");
        // GetAsync + ResponseHeadersRead (rather than GetStreamAsync) so the response headers are
        // visible below — FeedHealthCheckService uses ETag/Last-Modified to detect an upstream
        // feed that has stopped being regenerated. The body is still streamed, not buffered.
        using var response = await client.GetAsync(XmlUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var etag = response.Headers.ETag?.Tag;
        var lastModified = response.Content.Headers.LastModified;

        await using var xmlStream = await response.Content.ReadAsStreamAsync(ct);

        var rows = new List<EstablishmentSyncRow>(57_000);
        var settings = new XmlReaderSettings { Async = true };
        using var reader = XmlReader.Create(xmlStream, settings);

        var totalRowsSeen = 0;
        while (await reader.ReadAsync())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "Row")
                continue;

            totalRowsSeen++;

            // XNode.ReadFrom consumes the entire <Row>...</Row> at once,
            // avoiding the double-advance bug that occurs with ReadElementContentAsStringAsync
            // on compact (no-whitespace) XML.
            var element = (XElement)XNode.ReadFrom(reader);
            var row = ReadRow(element);
            if (row is not null)
                rows.Add(row);
        }

        logger.LogInformation(
            "Parsed {Count}/{Total} establishments from XML (ETag={ETag}, LastModified={LastModified}).",
            rows.Count, totalRowsSeen, etag, lastModified);
        return new FodevareFeedResult(rows, totalRowsSeen, etag, lastModified);
    }

    private static EstablishmentSyncRow? ReadRow(XElement e)
    {
        var idNummer = TryParseInt(e.Element("ID_nummer")?.Value) ?? 0;
        var name     = NullIfEmpty(e.Element("Virksomhed")?.Value);

        if (idNummer == 0 || name is null)
            return null;

        var scoreNames = new[] { "Seneste_kontrol_resultat",       "Næstseneste_kontrol_resultat",
                                 "Tredjeseneste_kontrol_resultat",  "Fjerdeseneste_kontrol_resultat" };
        var dateNames  = new[] { "Seneste_kontrol_dato",       "Næstseneste_kontrol_dato",
                                 "Tredjeseneste_kontrol_dato",  "Fjerdeseneste_kontrol_dato" };

        var latestScoreDate = TryParseDate(e.Element(dateNames[0])?.Value ?? "");

        var inspections = new List<(int Score, DateOnly Date)>(4);
        for (int i = 0; i < 4; i++)
        {
            var score = TryParseInt(e.Element(scoreNames[i])?.Value);
            var date  = i == 0 ? latestScoreDate : TryParseDate(e.Element(dateNames[i])?.Value ?? "");
            if (score.HasValue && date.HasValue)
                inspections.Add((score.Value, date.Value));
        }

        return new EstablishmentSyncRow(
            idNummer,
            NullIfEmpty(e.Element("CVR_nummer")?.Value),
            name,
            NullIfEmpty(e.Element("Adresse")?.Value),
            NullIfEmpty(e.Element("Postnummer")?.Value),
            NullIfEmpty(e.Element("By")?.Value),
            NullIfEmpty(e.Element("FVST_branchenummer")?.Value),
            NullIfEmpty(e.Element("FVST_branche")?.Value),
            // Geo_Lat/Geo_Lng no longer exist in this feed — always null now. Existing rows keep
            // their last-synced values until this MERGE touches them, then those go null too.
            null,
            null,
            // Built from ID_nummer rather than trusting the feed's own "URL" element, matching the
            // pre-existing self-heal pattern (ReportUrl is a pure function of the id — see
            // EstablishmentSyncService's MERGE) even though this feed's own URL now uses the same format.
            $"https://www.findsmiley.dk/app/{idNummer}",
            NullIfEmpty(e.Element("Virksomhedstype")?.Value),
            NullIfEmpty(e.Element("Smileybranche")?.Value),
            latestScoreDate,
            NullIfEmpty(e.Element("P_nummer")?.Value),
            inspections);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? TryParseInt(string? value) =>
        int.TryParse(value, out var i) ? i : null;

    private static DateOnly? TryParseDate(string? value) =>
        DateOnly.TryParseExact(value, "MM/dd/yyyy", null,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
}
