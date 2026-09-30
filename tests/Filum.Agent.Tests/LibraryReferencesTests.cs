using System.Xml.Linq;

namespace Filum.Agent.Tests;

/// <summary>The turn stays a library any host can use: no web framework in it, so a host brings its own.</summary>
public sealed class LibraryReferencesTests
{
    [Fact]
    public void The_turn_references_no_web()
    {
        Assert.DoesNotContain(typeof(FilumModel).Assembly.GetReferencedAssemblies(), a => (a.Name ?? string.Empty).StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));

        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "Filum.slnx")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("Filum.slnx not found above the test output.");
        }

        var packages = XDocument.Load(Path.Combine(folder, "src", "Filum.Agent", "Filum.Agent.csproj"))
            .Descendants("PackageReference")
            .Select(p => (string?)p.Attribute("Include") ?? string.Empty);
        Assert.DoesNotContain(packages, p => p.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));
    }
}
