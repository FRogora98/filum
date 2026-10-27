using Microsoft.ML.Tokenizers;

namespace Filum.Embeddings;

// XLM-RoBERTa's ids, which both models use: SentencePiece's shifted by one, with its unknown at 3.
internal static class XlmRobertaPieces
{
    public const long Begin = 0;
    public const long Pad = 1;
    public const long End = 2;
    private const long Unknown = 3;

    // The ids of a text, without begin and end, at most max.
    public static long[] Of(SentencePieceTokenizer tokenizer, string text, int max)
    {
        var pieces = tokenizer.EncodeToIds(text);
        var count = Math.Min(pieces.Count, Math.Max(0, max));
        var ids = new long[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = pieces[i] == 0 ? Unknown : pieces[i] + 1;
        }

        return ids;
    }
}
