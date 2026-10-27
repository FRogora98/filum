using Filum.Engine.Abstractions.Search;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Filum.Embeddings;

/// <summary>
/// <c>bge-reranker-v2-m3</c> (Apache-2.0, © BAAI; the int8 ONNX export of onnx-community/bge-reranker-v2-m3-ONNX), on
/// the CPU (spec 042): it reads a query and a text together, as XLM-RoBERTa's pair <c>&lt;s&gt; query &lt;/s&gt;&lt;/s&gt; text
/// &lt;/s&gt;</c>, and gives the logit of how well the text answers the query.
/// </summary>
public sealed class BgeReranker : IReranker, IDisposable
{
    /// <summary>The longest pair in tokens: the model reads 8,192, but a match's start says what it is about, and the time grows with the square.</summary>
    public const int MaxTokens = 256;

    // The query keeps at most this many tokens; the text has the rest.
    private const int MaxQueryTokens = 64;

    // Pairs scored at once: a bound on memory, since every pair of a batch is padded to the longest.
    private const int BatchSize = 8;

    private const string InputIds = "input_ids";
    private const string AttentionMask = "attention_mask";
    private const string TokenTypeIds = "token_type_ids";

    private readonly SentencePieceTokenizer _tokenizer;
    private readonly InferenceSession _session;
    private readonly bool _typeIds;
    private readonly Lock _gate = new();

    /// <summary>Loads the reranker from <paramref name="folder"/>, which must hold every file of <see cref="RerankerFiles"/>.</summary>
    public BgeReranker(string folder)
    {
        using (var model = File.OpenRead(Path.Combine(folder, RerankerFiles.Tokenizer)))
        {
            _tokenizer = SentencePieceTokenizer.Create(model, addBeginningOfSentence: false, addEndOfSentence: false);
        }

        _session = new InferenceSession(Path.Combine(folder, RerankerFiles.Model));
        _typeIds = _session.InputMetadata.ContainsKey(TokenTypeIds);
    }

    /// <summary>The reranker in <paramref name="folder"/>, or null when its files are not all there.</summary>
    public static BgeReranker? TryLoad(string folder) => RerankerFiles.Present(folder) ? new BgeReranker(folder) : null;

    /// <inheritdoc/>
    public IReadOnlyList<float>? Score(string query, IReadOnlyList<string> texts)
    {
        var scores = new List<float>(texts.Count);
        foreach (var batch in texts.Chunk(BatchSize))
        {
            scores.AddRange(Run([.. batch.Select(t => PairIds(query, t))]));
        }

        return scores;
    }

    /// <summary>The model's ids for a query and a text, cut to fit <see cref="MaxTokens"/>.</summary>
    public long[] PairIds(string query, string text)
    {
        var asked = XlmRobertaPieces.Of(_tokenizer, query, MaxQueryTokens);
        var said = XlmRobertaPieces.Of(_tokenizer, text, MaxTokens - asked.Length - 4);
        return [XlmRobertaPieces.Begin, .. asked, XlmRobertaPieces.End, XlmRobertaPieces.End, .. said, XlmRobertaPieces.End];
    }

    private float[] Run(List<long[]> ids)
    {
        var batch = ids.Count;
        var length = ids.Max(i => i.Length);
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

        List<NamedOnnxValue> inputs =
        [
            NamedOnnxValue.CreateFromTensor(InputIds, new DenseTensor<long>(input, [batch, length])),
            NamedOnnxValue.CreateFromTensor(AttentionMask, new DenseTensor<long>(mask, [batch, length]))
        ];
        if (_typeIds)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor(TokenTypeIds, new DenseTensor<long>(new long[batch * length], [batch, length])));
        }

        lock (_gate)
        {
            using var results = _session.Run(inputs);
            var logits = results[0].AsTensor<float>();
            return [.. Enumerable.Range(0, batch).Select(b => logits[b, 0])];
        }
    }

    /// <summary>Releases the model.</summary>
    public void Dispose() => _session.Dispose();
}
