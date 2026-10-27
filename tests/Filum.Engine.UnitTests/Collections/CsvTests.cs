using Filum.Engine.Collections;

namespace Filum.Engine.UnitTests.Collections;

public sealed class CsvTests
{
    [Fact]
    public void Check_WhenAHeaderAndRows_ShouldMakeACollection()
    {
        var table = Csv.Check("title,author,year\nFirst book,Someone,2024\nSecond book,Someone else,2025\n");

        Assert.True(table.IsValid);
        Assert.Equal(["title", "author", "year"], table.Header);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["Second book", "Someone else", "2025"], table.Rows[1].Fields);
        Assert.Equal(3, table.Rows[1].Line);
    }

    [Fact]
    public void Check_WhenFieldsAreQuoted_ShouldKeepCommasQuotesAndLineBreaks()
    {
        var table = Csv.Check("name,note\r\n\"Smith, J.\",\"said \"\"hello\"\"\"\r\nplain,\"two\nlines\"\r\nlast,x\r\n");

        Assert.True(table.IsValid, table.Error);
        Assert.Equal(["Smith, J.", "said \"hello\""], table.Rows[0].Fields);
        Assert.Equal(["plain", "two\nlines"], table.Rows[1].Fields);
        Assert.Equal(3, table.Rows[1].Line);
        Assert.Equal(5, table.Rows[2].Line);
    }

    [Fact]
    public void Check_WhenLinesAreBlank_ShouldNotCountThemAsRows()
    {
        var table = Csv.Check("a,b\n\n1,2\n\n");

        Assert.True(table.IsValid);
        Assert.Single(table.Rows);
        Assert.Equal(3, table.Rows[0].Line);
    }

    [Theory]
    [InlineData("a,b,c\n1,2,3\n1,2\n", "Line 3 has 2 fields, the header has 3.")]
    [InlineData("a,b,c\n1,2,3,4\n", "Line 2 has 4 fields, the header has 3.")]
    [InlineData("", "needs a header")]
    [InlineData("\n\n", "needs a header")]
    [InlineData("a,,c\n", "needs a name")]
    [InlineData("a,b,A\n", "more than once")]
    [InlineData("a,b\n\"open,2\n", "not closed")]
    [InlineData("a,b\nsay \"hi\",2\n", "quote inside an unquoted field")]
    [InlineData("a,b\n\"x\"y,2\n", "after a closing quote")]
    public void Check_WhenTheCollectionIsBroken_ShouldRefuseItNamingTheProblem(string content, string error)
    {
        var table = Csv.Check(content);

        Assert.False(table.IsValid);
        Assert.Contains(error, table.Error);
    }
}
