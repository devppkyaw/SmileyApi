using System.Xml;

namespace SmileyApi.Api.Workers;

public class FodevareXmlParser(IHttpClientFactory httpClientFactory, ILogger<FodevareXmlParser> logger)
{
    private const string XmlUrl = "https://www.foedevarestyrelsen.dk/Media/638212360788086849/Smiley_xml.xml";

    public async Task<IReadOnlyList<EstablishmentSyncRow>> ParseAsync(CancellationToken ct)
    {
        logger.LogInformation("Downloading Smiley XML from Fødevarestyrelsen...");

        var client = httpClientFactory.CreateClient();
        await using var xmlStream = await client.GetStreamAsync(XmlUrl, ct);

        var rows = new List<EstablishmentSyncRow>(55_000);
        var settings = new XmlReaderSettings { Async = true };

        using var reader = XmlReader.Create(xmlStream, settings);

        while (await reader.ReadAsync())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "row")
                continue;

            var row = await ReadRowAsync(reader);
            if (row is not null)
                rows.Add(row);
        }

        logger.LogInformation("Parsed {Count} establishments from XML.", rows.Count);
        return rows;
    }

    private static async Task<EstablishmentSyncRow?> ReadRowAsync(XmlReader reader)
    {
        int navnelbnr = 0;
        string? cvrNumber = null, name = null, address = null, postalCode = null;
        string? city = null, industryCode = null, industryName = null, reportUrl = null;
        double? geoLat = null, geoLng = null;

        // Parallel score/date slots from XML (index 0 = latest, 1–3 = older)
        var scores = new int?[4];
        var dates = new DateOnly?[4];

        var depth = reader.Depth;

        while (await reader.ReadAsync() && reader.Depth > depth)
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            var elementName = reader.Name;
            var value = await reader.ReadElementContentAsStringAsync();

            switch (elementName)
            {
                case "navnelbnr":    navnelbnr = TryParseInt(value) ?? 0; break;
                case "cvrnr":        cvrNumber = NullIfEmpty(value); break;
                case "navn":         name = NullIfEmpty(value); break;
                case "adresse1":     address = NullIfEmpty(value); break;
                case "postnr":       postalCode = NullIfEmpty(value); break;
                case "By":           city = NullIfEmpty(value); break;
                case "brancheKode":  industryCode = NullIfEmpty(value); break;
                case "branche":      industryName = NullIfEmpty(value); break;
                case "URL":          reportUrl = NullIfEmpty(value); break;
                case "Geo_Lat":      geoLat = TryParseDouble(value); break;
                case "Geo_Lng":      geoLng = TryParseDouble(value); break;

                case "seneste_kontrol":       scores[0] = TryParseInt(value); break;
                case "seneste_kontrol_dato":  dates[0]  = TryParseDate(value); break;
                case "seneste_kontrol1":      scores[1] = TryParseInt(value); break;
                case "seneste_kontrol_dato1": dates[1]  = TryParseDate(value); break;
                case "seneste_kontrol2":      scores[2] = TryParseInt(value); break;
                case "seneste_kontrol_dato2": dates[2]  = TryParseDate(value); break;
                case "seneste_kontrol3":      scores[3] = TryParseInt(value); break;
                case "seneste_kontrol_dato3": dates[3]  = TryParseDate(value); break;
            }
        }

        if (navnelbnr == 0 || string.IsNullOrWhiteSpace(name))
            return null;

        var inspections = new List<(int Score, DateOnly Date)>(4);
        for (int i = 0; i < 4; i++)
        {
            if (scores[i].HasValue && dates[i].HasValue)
                inspections.Add((scores[i]!.Value, dates[i]!.Value));
        }

        return new EstablishmentSyncRow(navnelbnr, cvrNumber, name, address, postalCode,
            city, industryCode, industryName, geoLat, geoLng, reportUrl, inspections);
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? TryParseInt(string value) =>
        int.TryParse(value, out var i) ? i : null;

    private static double? TryParseDouble(string value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    private static DateOnly? TryParseDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", null,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
}
