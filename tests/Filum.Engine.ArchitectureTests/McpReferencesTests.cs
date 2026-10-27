using System.Xml.Linq;

namespace Filum.Engine.ArchitectureTests;

public sealed class McpReferencesTests
{
    private static readonly string[] NotInTheServer = ["Microsoft.AspNetCore", "Microsoft.Extensions.Http", "ModelContextProtocol.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql", "OpenAI", "Microsoft.Agents", "Microsoft.Extensions.AI.OpenAI"];

    [Fact]
    public void Mcp_WhenBuilt_ShouldReferenceNoWebHttpDatabaseOrModelProvider()
    {
        var referenced = typeof(Filum.Mcp.McpInstructions).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);
        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "Filum.slnx")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("Filum.slnx not found above the test output.");
        }

        var packages = XDocument.Load(Path.Combine(folder, "src", "Filum.Mcp", "Filum.Mcp.csproj"))
            .Descendants("PackageReference")
            .Select(p => (string?)p.Attribute("Include") ?? string.Empty);

        Assert.DoesNotContain(referenced.Concat(packages), n => NotInTheServer.Any(p => n.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
    }
}
