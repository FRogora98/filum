namespace Filum.Engine.Storage.LocalFolder.UnitTests;

public sealed class FileMeasuresTests
{
    [Fact]
    public void CollectionShape_WhenTheFileIsAValidCollection_ShouldGiveItsHeaderAndRows()
    {
        Assert.Equal(("title,year", (int?)2), FileMeasures.CollectionShape("/lists/films.csv", "title,year\nDune,2021\nAlien,1979\n"));
    }

    [Fact]
    public void CollectionShape_WhenTheFileIsADocumentOrAnInvalidCollection_ShouldGiveNothing()
    {
        Assert.Equal(((string?)null, (int?)null), FileMeasures.CollectionShape("/notes/a.md", "a,b\n1,2\n"));
        Assert.Equal(((string?)null, (int?)null), FileMeasures.CollectionShape("/lists/bad.csv", "a,b\n1,2,3\n"));
    }

    [Fact]
    public void Hash_WhenTheContentChanges_ShouldChange()
    {
        Assert.Equal(FileMeasures.Hash("same"), FileMeasures.Hash("same"));
        Assert.NotEqual(FileMeasures.Hash("same"), FileMeasures.Hash("same "));
    }
}
