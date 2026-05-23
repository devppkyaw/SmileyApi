using System.Xml;
using System.Xml.Linq;

namespace SmilrApi.Api.Workers;

public class FodevareXmlParser(IHttpClientFactory httpClientFactory, ILogger<FodevareXmlParser> logger)
{
    private const string XmlUrl = "https://www.foedevarestyrelsen.dk/Media/638212360788086849/Smiley_xml.xml";

    public async Task<IReadOnlyList<EstablishmentSyncRow>> ParseAsync(CancellationToken ct)
    {
        logger.LogInformation("Downloading Smiley XML from Fødevarestyrelsen...");

        var client = httpClientFactory.CreateClient();
        await using var xmlStream = await client.GetStreamAsync(XmlUrl, ct);

        var rows = new List<EstablishmentSyncRow>(57_000);
        var settings = new XmlReaderSettings { Async = true };
        using var reader = XmlReader.Create(xmlStream, settings);

        while (await reader.ReadAsync())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "row")
                continue;

            // XNode.ReadFrom consumes the entire <row>...</row> at once,
            // avoiding the double-advance bug that occurs with ReadElementContentAsStringAsync
            // on compact (no-whitespace) XML.
            var element = (XElement)XNode.ReadFrom(reader);
            var row = ReadRow(element);
            if (row is not null)
                rows.Add(row);
        }

        logger.LogInformation("Parsed {Count} establishments from XML.", rows.Count);
        return rows;
    }

    private static EstablishmentSyncRow? ReadRow(XElement e)
    {
        var navnelbnr = TryParseInt(e.Element("navnelbnr")?.Value) ?? 0;
        var name      = NullIfEmpty(e.Element("navn1")?.Value);

        if (navnelbnr == 0 || name is null)
            return null;

        var scoreNames = new[] { "seneste_kontrol",       "naestseneste_kontrol",
                                 "tredjeseneste_kontrol",  "fjerdeseneste_kontrol" };
        var dateNames  = new[] { "seneste_kontrol_dato",       "naestseneste_kontrol_dato",
                                 "tredjeseneste_kontrol_dato",  "fjerdeseneste_kontrol_dato" };

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
            navnelbnr,
            NullIfEmpty(e.Element("cvrnr")?.Value),
            name,
            NullIfEmpty(e.Element("adresse1")?.Value),
            NullIfEmpty(e.Element("postnr")?.Value),
            NullIfEmpty(e.Element("By")?.Value),
            NullIfEmpty(e.Element("brancheKode")?.Value),
            NullIfEmpty(e.Element("branche")?.Value),
            TryParseDouble(e.Element("Geo_Lat")?.Value ?? ""),
            TryParseDouble(e.Element("Geo_Lng")?.Value ?? ""),
            NullIfEmpty(e.Element("URL")?.Value),
            NullIfEmpty(e.Element("virksomhedstype")?.Value),
            NullIfEmpty(e.Element("Pixibranche")?.Value),
            latestScoreDate,
            NullIfEmpty(e.Element("pnr")?.Value),
            inspections);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? TryParseInt(string? value) =>
        int.TryParse(value, out var i) ? i : null;

    private static double? TryParseDouble(string? value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    private static DateOnly? TryParseDate(string? value) =>
        DateOnly.TryParseExact(value, "dd-MM-yyyy HH:mm:ss", null,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
}
