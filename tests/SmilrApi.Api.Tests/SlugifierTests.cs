using SmilrApi.Core.Utils;

namespace SmilrApi.Api.Tests;

public class SlugifierTests
{
    [Theory]
    [InlineData("København", "koebenhavn")]
    [InlineData("København Ø", "koebenhavn-oe")]
    [InlineData("Århus C", "aarhus-c")]
    [InlineData("Ålborg", "aalborg")]
    public void Slugify_replaces_danish_letters(string input, string expected)
    {
        Assert.Equal(expected, Slugifier.Slugify(input));
    }

    [Theory]
    [InlineData("Café Rï", "cafe-ri")]
    [InlineData("Müller's Bäckerei", "muller-s-backerei")]
    public void Slugify_folds_other_accented_letters(string input, string expected)
    {
        Assert.Equal(expected, Slugifier.Slugify(input));
    }

    [Theory]
    [InlineData("7-Eleven", "7-eleven")]
    [InlineData("McDonald's #1234", "mcdonald-s-1234")]
    public void Slugify_keeps_digits_and_collapses_punctuation(string input, string expected)
    {
        Assert.Equal(expected, Slugifier.Slugify(input));
    }

    [Theory]
    [InlineData("  Pizza   Palace!!  ", "pizza-palace")]
    [InlineData("-leading-and-trailing-", "leading-and-trailing")]
    public void Slugify_trims_and_collapses_whitespace_and_symbols(string input, string expected)
    {
        Assert.Equal(expected, Slugifier.Slugify(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("***")]
    [InlineData("🍕🍕")]
    public void Slugify_falls_back_when_nothing_survives(string? input)
    {
        Assert.Equal("x", Slugifier.Slugify(input));
    }

    [Fact]
    public void Slugify_uses_custom_fallback()
    {
        Assert.Equal("virksomhed", Slugifier.Slugify("", fallback: "virksomhed"));
    }
}
