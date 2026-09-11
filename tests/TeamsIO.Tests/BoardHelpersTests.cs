using Xunit;

public sealed class BoardHelpersTests
{
    [Fact]
    public void StripHtml_RemovesMarkupAndDecodesEntities()
    {
        var result = BoardHelpers.StripHtml("<p>Working &amp; available</p>");

        Assert.Equal("Working & available", result);
    }

    [Fact]
    public void StripHtml_ReturnsDashForMissingStatus()
    {
        Assert.Equal("—", BoardHelpers.StripHtml(null));
    }

    [Fact]
    public void Truncate_AddsEllipsisAtRequestedWidth()
    {
        var result = BoardHelpers.Truncate("abcdefgh", 5);

        Assert.Equal("abcd…", result);
        Assert.Equal(5, result.Length);
    }
}
