namespace Filum.Engine.Storage.LocalFolder.UnitTests;

public sealed class JsonLinesTests
{
    [Fact]
    public void Parse_WhenALineIsEmptyOrCutShort_ShouldSkipItAndKeepTheOthersInOrder()
    {
        var lines = JsonLines.Parse<Line>(["""{"id":1,"v":"a"}""", "", """{"id":2,"v":"b""", """{"id":3,"v":"c"}"""]);

        Assert.Equal([new Line(1, "a"), new Line(3, "c")], lines);
    }

    [Fact]
    public void Serialize_WhenWritingALine_ShouldUseCamelCaseNamesOnOneLine()
    {
        Assert.Equal("""{"id":4,"v":"d"}""", JsonLines.Serialize(new Line(4, "d")));
    }

    private sealed record Line(long Id, string V);
}
