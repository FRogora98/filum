using System.Text.Json;
using Filum.Engine.Abstractions.Search;

namespace Filum.Embeddings;

/// <summary>
/// Keep or discard: a logistic regression on the vector of <see cref="E5Embedder"/>, trained on LongMemEval
/// `S` (the person's messages of its personal sessions against those of its generic ones), with the threshold that kept
/// at least 98% of the messages that hold an answer. Its weights ship with this library (<c>keep-gate.json</c>).
/// </summary>
public sealed class KeepGate(float[] weights, float bias, float threshold) : IEventGate
{
    private const string ShippedResource = "Filum.Embeddings.keep-gate.json";

    // The training's defaults: the share of the messages that must be kept, and the gradient descent's settings.
    private const double DefaultRecall = 0.98;
    private const int DefaultEpochs = 600;
    private const float DefaultRate = 2f;
    private const float DefaultL2 = 1e-4f;

    // A logistic regression's even odds: the threshold before it is lowered to keep the recall, and its ceiling.
    private const float EvenOdds = 0.5f;

    /// <summary>The regression's weights, one per dimension of the vector.</summary>
    public float[] Weights => weights;

    /// <summary>The regression's bias.</summary>
    public float Bias => bias;

    /// <summary>The score from which an event is kept.</summary>
    public float Threshold => threshold;

    /// <summary>The probability that the vector says something about the person's life.</summary>
    public float Score(float[] vector) => 1f / (1f + MathF.Exp(-(Dot(weights, vector) + bias)));

    /// <inheritdoc/>
    public bool Keep(float[] vector) => Score(vector) >= threshold;

    private static float Dot(float[] a, float[] b)
    {
        var sum = 0f;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    private sealed record Saved(float[] Weights, float Bias, float Threshold);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The gate as JSON, as <c>keep-gate.json</c> keeps it.</summary>
    public string ToJson() => JsonSerializer.Serialize(new Saved(weights, bias, threshold), Json);

    /// <summary>A gate read from <paramref name="json"/>, as <see cref="ToJson"/> writes it.</summary>
    public static KeepGate FromJson(string json)
    {
        var saved = JsonSerializer.Deserialize<Saved>(json, Json) ?? throw new InvalidDataException("The gate's weights are empty.");
        return new KeepGate(saved.Weights, saved.Bias, saved.Threshold);
    }

    /// <summary>The gate that ships with the model, or null when this build has none.</summary>
    public static KeepGate? Shipped()
    {
        using var stream = typeof(KeepGate).Assembly.GetManifestResourceStream(ShippedResource);
        return stream is null ? null : FromJson(new StreamReader(stream).ReadToEnd());
    }

    /// <summary>
    /// Trains the gate (logistic regression, full-batch gradient descent with a little L2) on vectors labelled keep or
    /// discard, and sets the threshold that keeps <paramref name="recall"/> of <paramref name="mustKeep"/>.
    /// </summary>
    public static KeepGate Train(IReadOnlyList<(float[] Vector, bool Keep)> examples, IReadOnlyList<float[]> mustKeep, double recall = DefaultRecall, int epochs = DefaultEpochs, float rate = DefaultRate, float l2 = DefaultL2)
    {
        var dimensions = examples[0].Vector.Length;
        var w = new float[dimensions];
        var b = 0f;
        var grad = new float[dimensions];
        for (var epoch = 0; epoch < epochs; epoch++)
        {
            Array.Clear(grad);
            var gb = 0f;
            foreach (var (x, y) in examples)
            {
                var p = 1f / (1f + MathF.Exp(-(Dot(w, x) + b)));
                var e = p - (y ? 1f : 0f);
                for (var d = 0; d < dimensions; d++)
                {
                    grad[d] += e * x[d];
                }

                gb += e;
            }

            for (var d = 0; d < dimensions; d++)
            {
                w[d] -= rate * (grad[d] / examples.Count + l2 * w[d]);
            }

            b -= rate * gb / examples.Count;
        }

        var gate = new KeepGate(w, b, EvenOdds);
        if (mustKeep.Count == 0)
        {
            return gate;
        }

        var scores = mustKeep.Select(gate.Score).Order().ToList();
        var index = (int)Math.Floor((1 - recall) * scores.Count);
        return new KeepGate(w, b, Math.Min(EvenOdds, scores[Math.Clamp(index, 0, scores.Count - 1)]));
    }
}
