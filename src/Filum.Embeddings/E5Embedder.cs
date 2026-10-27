using Filum.Engine.Abstractions.Search;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.Tokenizers;

namespace Filum.Embeddings;

/// <summary>
/// <c>multilingual-e5-small</c> (MIT, © its authors; the ONNX export of Xenova/multilingual-e5-small), quantized, on the
/// CPU: SentencePiece tokens with the XLM-R ids, the mean of the last hidden states over the tokens, made
/// unit length. Texts that ask are prefixed "query: ", texts that were said "passage: ", as the model was trained.
/// </summary>
public sealed class E5Embedder : IEmbedder, IDisposable
{
    /// <summary>
    /// The longest input in tokens: the model reads 512, but what a message is about is in its start, and half the
    /// length makes it about three times faster.
    /// </summary>
    public const int MaxTokens = 256;

    // The prefixes the model was trained with: a text that asks, and a text that was said.
    private const string QueryPrefix = "query: ";
    private const string PassagePrefix = "passage: ";

    // The names of the model's inputs in its ONNX graph.
    private const string InputIds = "input_ids";
    private const string AttentionMask = "attention_mask";
    private const string TokenTypeIds = "token_type_ids";

    private readonly SentencePieceTokenizer _tokenizer;
    private readonly InferenceSession _session;
    private readonly Lock _gate = new();

    /// <summary>Loads the model from <paramref name="folder"/>, which must hold every file of <see cref="ModelFiles"/>.</summary>
    public E5Embedder(string folder)
    {
        using (var model = File.OpenRead(Path.Combine(folder, ModelFiles.Tokenizer)))
        {
            _tokenizer = SentencePieceTokenizer.Create(model, addBeginningOfSentence: false, addEndOfSentence: false);
        }

        _session = new InferenceSession(Path.Combine(folder, ModelFiles.Model));
    }

    /// <summary>The model in <paramref name="folder"/>, or null when its files are not all there.</summary>
    public static E5Embedder? TryLoad(string folder) => ModelFiles.Present(folder) ? new E5Embedder(folder) : null;

    /// <inheritdoc/>
    public string ModelId => $"{ModelFiles.Name}@{ModelFiles.Revision}";

    /// <inheritdoc/>
    public IReadOnlyList<float[]> Embed(IReadOnlyList<string> texts, bool query)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        var ids = texts.Select(t => Ids((query ? QueryPrefix : PassagePrefix) + t)).ToList();
        var length = ids.Max(i => i.Length);
        using var results = Run(ids, length);
        var hidden = results[0].AsTensor<float>();
        return [.. ids.Select((tokens, b) => MeanUnitVector(hidden, b, tokens.Length))];
    }

    private IDisposableReadOnlyCollection<DisposableNamedOnnxValue> Run(List<long[]> ids, int length)
    {
        var batch = ids.Count;
        var input = new long[batch * length];
        var mask = new long[batch * length];
        for (var b = 0; b < batch; b++)
        {
            for (var t = 0; t < length; t++)
            {
                input[b * length + t] = t < ids[b].Length ? ids[b][t] : XlmRobertaPieces.Pad;
                mask[b * length + t] = t < ids[b].Length ? 1 : 0;
            }
        }

        lock (_gate)
        {
            return _session.Run([
                NamedOnnxValue.CreateFromTensor(InputIds, new DenseTensor<long>(input, [batch, length])),
                NamedOnnxValue.CreateFromTensor(AttentionMask, new DenseTensor<long>(mask, [batch, length])),
                NamedOnnxValue.CreateFromTensor(TokenTypeIds, new DenseTensor<long>(new long[batch * length], [batch, length]))
            ]);
        }
    }

    private static float[] MeanUnitVector(Tensor<float> hidden, int b, int count)
    {
        var dimensions = hidden.Dimensions[2];
        var vector = new float[dimensions];
        for (var t = 0; t < count; t++)
        {
            for (var d = 0; d < dimensions; d++)
            {
                vector[d] += hidden[b, t, d];
            }
        }

        var norm = 0f;
        for (var d = 0; d < dimensions; d++)
        {
            vector[d] /= count;
            norm += vector[d] * vector[d];
        }

        norm = MathF.Sqrt(norm);
        for (var d = 0; d < dimensions; d++)
        {
            vector[d] /= norm;
        }

        return vector;
    }

    /// <summary>The model's ids: SentencePiece's shifted by one (its unknown is 3), between begin and end, cut to fit.</summary>
    public long[] Ids(string text) => [XlmRobertaPieces.Begin, .. XlmRobertaPieces.Of(_tokenizer, text, MaxTokens - 2), XlmRobertaPieces.End];

    /// <summary>Releases the model.</summary>
    public void Dispose() => _session.Dispose();
}
