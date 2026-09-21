using SmilrApi.Api.Rendering;
using SmilrApi.Core.Models;

namespace SmilrApi.Api.Tests;

public class DetailPageMapLinkTests
{
    private const string MapPrefix = "https://www.google.com/maps/search/?api=1&query=";

    private static string Render(Establishment est) =>
        FindPageRenderer.DetailPage(est, [], [], []);

    [Fact]
    public void MapLink_SearchesByNameAndAddress_NotCoordinates()
    {
        var html = Render(new Establishment
        {
            Navnelbnr = 1, Name = "Café Æblet", Address = "Testvej 1", PostalCode = "2200", City = "København N",
            GeoLat = 55.7, GeoLng = 12.5
        });

        var expected = MapPrefix + Uri.EscapeDataString("Café Æblet Testvej 1 2200 København N");
        Assert.Contains($"href=\"{expected}\"", html);
        Assert.DoesNotContain("query=55.7", html);
    }

    [Fact]
    public void MapLink_FallsBackToCoordinates_WhenNoAddress()
    {
        var html = Render(new Establishment { Navnelbnr = 2, Name = "No Address", GeoLat = 55.7, GeoLng = 12.5 });

        Assert.Contains($"href=\"{MapPrefix}55.7,12.5\"", html);
    }

    [Fact]
    public void MapLink_OpensInNewTab_WithNoopener()
    {
        var html = Render(new Establishment { Navnelbnr = 3, Name = "X", Address = "Y 1", City = "Z" });

        Assert.Matches("<a href=\"https://www.google.com/maps/[^\"]*\" target=\"_blank\" rel=\"noopener noreferrer\">", html);
    }
}
