using System.Xml.Linq;

namespace Filum.Mcp.Tests;

/// <summary>filum-mcp opens no network connection and needs no model: it asks for no web, HTTP, database or model package.</summary>
public sealed class ReferencesTests
{
    private static readonly string[] NotInTheServer = ["Microsoft.AspNetCore", "Microsoft.Extensions.Http", "ModelContextProtocol.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql", "OpenAI", "Microsoft.Agents", "Microsoft.Extensions.AI.OpenAI"];

    [Fact]
    public void The_server_references_no_web_http_database_or_model_provider()
    {
        var referenced = typeof(McpInstructions).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);
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
