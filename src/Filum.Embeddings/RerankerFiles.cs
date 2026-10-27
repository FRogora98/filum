namespace Filum.Embeddings;

/// <summary>
/// The reranker's files (spec 042), where they come from, and their hashes: a download is used only when it matches.
/// About 575 MB, downloaded only when a host asks for the reranker.
/// </summary>
public static class RerankerFiles
{
    /// <summary>The model's name, also the name of the folder it is kept in.</summary>
    public const string Name = "bge-reranker-v2-m3";

    /// <summary>The model's ONNX file: the int8 export.</summary>
    public const string Model = "model_quantized.onnx";

    /// <summary>The tokenizer's file: the same file as the e5 model's (XLM-RoBERTa's SentencePiece model).</summary>
    public const string Tokenizer = "sentencepiece.bpe.model";

    /// <summary>The revision of the ONNX export's repository the model's hash belongs to.</summary>
    public const string ModelRevision = "6f5ff65298512715a1e669753bc754d2bc8f367b";

    /// <summary>The revision of the original model's repository the tokenizer's hash belongs to.</summary>
    public const string TokenizerRevision = "953dc6f6f85a1b2dbfca4c34a2796e7dde08d41e";

    /// <summary>Each file, with where it is downloaded from and its SHA-256 hash.</summary>
    public static readonly IReadOnlyDictionary<string, (string Url, string Sha256)> Files = new Dictionary<string, (string, string)>
    {
        [Model] = ($"https://huggingface.co/onnx-community/bge-reranker-v2-m3-ONNX/resolve/{ModelRevision}/onnx/{Model}", "912fc1215c2dbff6499700534bd8d31253af01573861abbfc43afd1fab6cce5d"),
        [Tokenizer] = ($"https://huggingface.co/BAAI/bge-reranker-v2-m3/resolve/{TokenizerRevision}/{Tokenizer}", "cfc8146abe2a0488e9e2a0c56de7952f7c11ab059eca145a0a727afce0db2865")
    };

    /// <summary>Whether every file of the reranker is in <paramref name="folder"/>.</summary>
    public static bool Present(string folder) => Files.Keys.All(f => File.Exists(Path.Combine(folder, f)));

    /// <summary>
    /// Downloads the files that are missing into <paramref name="folder"/>, each kept only when its hash matches; a
    /// partial download never takes a file's name. Returns whether the reranker is complete.
    /// </summary>
    public static Task<bool> EnsureAsync(string folder, HttpClient http, CancellationToken cancellationToken) =>
        Downloads.EnsureAsync(folder, Files, http, cancellationToken);
}
