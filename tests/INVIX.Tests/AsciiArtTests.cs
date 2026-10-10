using Invix.Assets;
using Xunit;

namespace Invix.Tests;

public class AsciiArtTests
{
    [Fact]
    public void Logo_lines_all_share_the_declared_width()
    {
        Assert.Equal(5, AsciiArt.LogoLines.Length);
        Assert.All(AsciiArt.LogoLines, line => Assert.Equal(AsciiArt.LogoWidth, line.Length));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(50.0, 12)]
    [InlineData(100.0, 24)]
    public void Bar_fills_the_expected_number_of_cells(double percent, int expectedFilled)
    {
        var bar = AsciiArt.Bar(percent);

        Assert.Equal('[', bar[0]);
        Assert.Equal(']', bar[^1]);
        Assert.Equal(expectedFilled, bar.Count(c => c == '#'));
        Assert.Equal(24, bar.Count(c => c is '#' or '.'));
    }

    [Fact]
    public void Bar_clamps_values_outside_zero_to_hundred()
    {
        Assert.Equal(24, AsciiArt.Bar(180).Count(c => c == '#'));
        Assert.Equal(0, AsciiArt.Bar(-40).Count(c => c == '#'));
    }

    [Fact]
    public void Bar_renders_a_placeholder_for_missing_samples()
    {
        Assert.Equal(0, AsciiArt.Bar(double.NaN).Count(c => c == '#'));
    }

    [Fact]
    public void Branding_is_INVIX_only()
    {
        Assert.Equal("INVI X", AsciiArt.Wordmark);
        Assert.Contains("INVI X", AsciiArt.Footer);
        Assert.Contains("PRECISION MODE", AsciiArt.Footer);
        Assert.Contains("GAMING EDITION", AsciiArt.Footer);
        Assert.Equal("ULTRA LOW LATENCY GAMING ENGINE", AsciiArt.Subtitle);
    }
}