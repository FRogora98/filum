using System.Text.Json.Nodes;
using System.Text.Json;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.IntegrationTests.Snapshots;
using Filum.Engine.Testing.Builders;

namespace Filum.Engine.IntegrationTests.Tools;

// filum-mcp's tests read the same snapshot.
public sealed class ToolCatalogTests
{
    [Fact]
    public void Catalog_WhenWrittenAsJson_ShouldMatchItsSnapshot()
    {
        var catalog = new JsonArray(ToolCatalogs.Of(new MemoryOptions())
            .Select(t => (JsonNode)new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["parameters"] = JsonNode.Parse(t.JsonSchema.GetRawText())
            })
            .ToArray());
        var text = catalog.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).Replace("\r\n", "\n") + "\n";

        // A deliberate change to the surface: FILUM_UPDATE_SNAPSHOT=1 dotnet test, then review the diff.
        SnapshotFile.Match("ToolCatalog.snapshot.json", text);
    }
}
