using System.Reflection;
using System.Xml.Linq;

namespace Filum.Engine.Tests;

/// <summary>
/// The engine stays a library any host can use: no web, no database framework, no model provider.
/// </summary>
public sealed class LibraryReferencesTests
{
    private static readonly string[] NotInTheEngine = ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql", "OpenAI", "Microsoft.Agents"];

    [Fact]
    public void The_engine_references_no_web_database_or_model_provider()
    {
        Assert.Empty(Forbidden(typeof(MemoryService).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty)));
        Assert.Empty(Forbidden(PackagesOfTheEngine()));
    }

    private static IEnumerable<string> Forbidden(IEnumerable<string> names) =>
        names.Where(n => NotInTheEngine.Any(p => n.StartsWith(p, StringComparison.OrdinalIgnoreCase))).ToList();

    /// <summary>The packages the engine's project asks for, read from its project file.</summary>
    private static IEnumerable<string> PackagesOfTheEngine()
    {
        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "Filum.slnx")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("Filum.slnx not found above the test output.");
        }

        return XDocument.Load(Path.Combine(folder, "src", "Filum.Engine", "Filum.Engine.csproj"))
            .Descendants("PackageReference")
            .Select(p => (string?)p.Attribute("Include") ?? string.Empty)
            .ToList();
    }
}
