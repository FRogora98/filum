namespace Filum.Embeddings;

/// <summary>The model's files, where they come from, and their hashes: a download is used only when it matches.</summary>
public static class ModelFiles
{
    /// <summary>The model's name, also the name of the folder it is kept in.</summary>
    public const string Name = "multilingual-e5-small";

    /// <summary>The model's ONNX file.</summary>
    public const string Model = "model_quantized.onnx";

    /// <summary>The tokenizer's file.</summary>
    public const string Tokenizer = "sentencepiece.bpe.model";

    /// <summary>The revision of the model's repository the hashes belong to.</summary>
    public const string Revision = "761b726dd34fb83930e26aab4e9ac3899aa1fa78";

    /// <summary>Each file, with where it is downloaded from and its SHA-256 hash.</summary>
    public static readonly IReadOnlyDictionary<string, (string Url, string Sha256)> Files = new Dictionary<string, (string, string)>
    {
        [Model] = ($"https://huggingface.co/Xenova/multilingual-e5-small/resolve/{Revision}/onnx/{Model}", "f80102d3f2a1229f387d3c81909990d8945513e347b0eab049f7de3c6f98c193"),
        [Tokenizer] = ($"https://huggingface.co/Xenova/multilingual-e5-small/resolve/{Revision}/{Tokenizer}", "cfc8146abe2a0488e9e2a0c56de7952f7c11ab059eca145a0a727afce0db2865")
    };

    /// <summary>Whether every file of the model is in <paramref name="folder"/>.</summary>
    public static bool Present(string folder) => Files.Keys.All(f => File.Exists(Path.Combine(folder, f)));

    /// <summary>
    /// Downloads the files that are missing into <paramref name="folder"/>, each kept only when its hash matches; a
    /// partial download never takes a file's name. Returns whether the model is complete.
    /// </summary>
    public static Task<bool> EnsureAsync(string folder, HttpClient http, CancellationToken cancellationToken) =>
        Downloads.EnsureAsync(folder, Files, http, cancellationToken);
}
