using Filum.Engine.Events;

namespace Filum.Engine.UnitTests.Events;

// Spec 057: which messages a listing takes, and the part of each it shows.
public sealed class EventListingTests
{
    [Theory]
    [InlineData("I fixed two bookshelves today", "bookshelf", true)]
    [InlineData("Two new batteries", "battery", true)]
    [InlineData("A new desk lamp arrived", "desk lamp", true)]
    [InlineData("A new desk arrived", "desk lamp", false)]
    [InlineData("We met at the TV studio", "tv", true)]
    [InlineData("The tvs were on", "tv", false)]
    public void Mentions_WhenAWordStartsAWordOfTheText_ShouldMatchEvenAPlural(string text, string alternative, bool expected) =>
        Assert.Equal(expected, EventSearch.Mentions(text, alternative));

    [Fact]
    public void Alternatives_WhenTheQueryHoldsBars_ShouldSplitThemAndDropEmptyOnes() =>
        Assert.Equal(["furniture", "book shelf", "sofa"], EventSearch.Alternatives("furniture | book shelf || sofa | ,"));

    [Fact]
    public void Around_WhenTheMatchIsFarIntoALongMessage_ShouldShowThePartAroundItNotTheStart()
    {
        var text = new string('x', 500) + " and then I bought a walnut bookshelf for 120 dollars " + new string('y', 500);

        var excerpt = EventSearch.Around(text, "bookshelf | sofa", 80);

        Assert.Contains("bookshelf", excerpt);
        Assert.StartsWith("…", excerpt);
        Assert.EndsWith("…", excerpt);
        Assert.True(excerpt.Length <= 82, excerpt);
        Assert.Equal("short and plain", EventSearch.Around("short and\nplain", "plain", 80));
    }
}
