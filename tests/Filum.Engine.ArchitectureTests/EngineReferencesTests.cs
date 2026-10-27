using System.Xml.Linq;
using Filum.Engine.Memory;

namespace Filum.Engine.ArchitectureTests;

public sealed class EngineReferencesTests
{
    private static readonly string[] NotInTheEngine = ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql", "OpenAI", "Microsoft.Agents"];

    [Fact]
    public void Engine_WhenBuilt_ShouldReferenceNoWebDatabaseOrModelProvider()
    {
        Assert.Empty(Forbidden(typeof(IMemoryService).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty)));
        Assert.Empty(Forbidden(PackagesOfTheEngine()));
    }

    private static IEnumerable<string> Forbidden(IEnumerable<string> names) =>
        names.Where(n => NotInTheEngine.Any(p => n.StartsWith(p, StringComparison.OrdinalIgnoreCase))).ToList();

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
