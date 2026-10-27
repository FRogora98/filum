namespace Filum.Engine.Storage.LocalFolder.UnitTests;

public sealed class VectorTextTests
{
    [Fact]
    public void FromText_WhenGivenWhatToTextWrote_ShouldGiveTheSameVector()
    {
        float[] vector = [0.5f, -1.25f, 3e-7f, 0f];

        Assert.Equal(vector, VectorText.FromText(VectorText.ToText(vector)));
    }

    [Fact]
    public void ToText_WhenWritingAVector_ShouldBeTheBase64OfItsFloats()
    {
        Assert.Equal(Convert.ToBase64String(BitConverter.GetBytes(1f)), VectorText.ToText([1f]));
    }
}
