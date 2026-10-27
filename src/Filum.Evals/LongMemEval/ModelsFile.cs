using System.Text.Json;

namespace Filum.Evals.LongMemEval;

internal sealed class ModelsFile
{
    public const string DefaultProvider = "openai";

    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private readonly JsonElement _root;

    private ModelsFile(string path, JsonElement root)
    {
        FilePath = path;
        _root = root;
    }

    public string FilePath { get; }

    public static ModelsFile Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path), Options);
        return new ModelsFile(path, document.RootElement.Clone());
    }

    public IReadOnlyDictionary<string, ModelPrice> Prices() =>
        Models().ToDictionary(m => m.GetProperty(EvalsConstants.ModelsFileFields.Id).GetString() ?? throw new InvalidDataException($"A model in {FilePath} has no Id."), PriceOf);

    public JsonElement? Find(string id) =>
        Models().FirstOrDefault(m => m.GetProperty(EvalsConstants.ModelsFileFields.Id).GetString() == id) is { ValueKind: not JsonValueKind.Undefined } model ? model : null;

    public static ModelPrice PriceOf(JsonElement model) =>
        new(model.GetProperty(EvalsConstants.ModelsFileFields.InputPrice).GetDecimal(), model.GetProperty(EvalsConstants.ModelsFileFields.OutputPrice).GetDecimal());

    public string? BaseUrl(string provider) =>
        _root.TryGetProperty(EvalsConstants.ModelsFileFields.Providers, out var providers) && providers.TryGetProperty(provider, out var settings) && settings.TryGetProperty(EvalsConstants.ModelsFileFields.BaseUrl, out var url) ? url.GetString() : null;

    private IEnumerable<JsonElement> Models() => _root.GetProperty(EvalsConstants.ModelsFileFields.Models).EnumerateArray();
}
