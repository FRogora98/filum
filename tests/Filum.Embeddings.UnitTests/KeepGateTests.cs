
namespace Filum.Embeddings.UnitTests;

public sealed class KeepGateTests
{
    [Fact]
    public void Shipped_WhenLoaded_ShouldHaveTheModelsDimensionsAndAThreshold()
    {
        var gate = KeepGate.Shipped();

        Assert.NotNull(gate);
        Assert.Equal(384, gate.Weights.Length);
        Assert.InRange(gate.Threshold, 0.01f, 0.5f);
    }

    [Fact]
    public void Train_WhenGivenTwoKinds_ShouldSeparateThemAndSetTheThresholdThatKeepsWhatMustBeKept()
    {
        var random = new Random(1);
        float[] Point(bool keep) => [keep ? 1f : -1f + (float)random.NextDouble() * 0.1f, (float)random.NextDouble()];
        var examples = Enumerable.Range(0, 200).Select(i => (Point(i % 2 == 0), i % 2 == 0)).ToList();

        var gate = KeepGate.Train(examples, [[0.9f, 0.5f], [1f, 0f]], recall: 1.0);

        Assert.True(gate.Keep([1f, 0.3f]));
        Assert.False(gate.Keep([-1f, 0.3f]));
        Assert.Equal(KeepGate.FromJson(gate.ToJson()).Bias, gate.Bias);
    }
}
