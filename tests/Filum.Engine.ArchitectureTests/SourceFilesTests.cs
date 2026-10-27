using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Filum.Engine.ArchitectureTests;

// BUILD-02: the size and layout rules of the standard that an analyzer does not check (SVC-02, CS-03), read from the
// sources with the C# parser, so a file over the limits fails the build's tests rather than waiting for an audit.
public sealed class SourceFilesTests
{
    private const int MaxFileLines = 300;
    private const int MaxMemberLines = 50;
    private static readonly string[] Roots = ["src", "samples", "tests"];
    private static readonly string[] SkippedFolders = ["bin", "obj"];

    [Fact]
    public void SourceFiles_WhenParsed_ShouldStayWithinTheFileLimit()
    {
        var tooLong = Sources()
            .Select(s => (s.Path, Lines: s.Tree.GetText().Lines.Count))
            .Where(f => f.Lines > MaxFileLines)
            .Select(f => $"{f.Path}: {f.Lines} lines");

        NoneOf(tooLong);
    }

    [Fact]
    public void Members_WhenParsed_ShouldStayWithinTheMethodLimit()
    {
        var tooLong = Sources()
            .SelectMany(s => s.Tree.GetRoot().DescendantNodes().Where(IsMethodLike).Select(n => (s.Path, Node: n)))
            .Select(m => (m.Path, m.Node, Lines: LineCount(m.Node)))
            .Where(m => m.Lines > MaxMemberLines)
            .Select(m => $"{m.Path}:{m.Node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {m.Lines} lines");

        NoneOf(tooLong);
    }

    [Fact]
    public void SourceFiles_WhenParsed_ShouldDeclareOneTopLevelTypeNamedLikeThePublicOne()
    {
        var wrong = new List<string>();
        foreach (var (path, tree) in Sources())
        {
            var types = TopLevelTypes(tree.GetRoot()).ToList();
            if (types.Count > 1)
            {
                wrong.Add($"{path}: {types.Count} top-level types");
            }

            var fileName = Path.GetFileName(path).Split('.')[0];
            wrong.AddRange(types
                .Where(t => t.Modifiers.Any(SyntaxKind.PublicKeyword) && NameOf(t) != fileName)
                .Select(t => $"{path}: public type {NameOf(t)} in a file named {fileName}"));
        }

        NoneOf(wrong);
    }

    [Fact]
    public void SourceFiles_WhenParsed_ShouldDeclareTheNamespaceOfTheirFolder()
    {
        // CS-02: each project's root namespace is its folder's name, and below it the namespace follows the folders.
        var wrong = new List<string>();
        foreach (var (path, tree) in Sources().Where(s => s.Path.StartsWith("src" + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            var declared = tree.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
            var folders = Path.GetDirectoryName(path)!.Split(Path.DirectorySeparatorChar).Skip(1);
            var expected = string.Join('.', folders);
            if (declared is not null && declared != expected)
            {
                wrong.Add($"{path}: {declared}, expected {expected}");
            }
        }

        NoneOf(wrong);
    }

    private static void NoneOf(IEnumerable<string> violations)
    {
        var list = violations.ToList();
        Assert.True(list.Count == 0, string.Join(Environment.NewLine, list));
    }

    private static bool IsMethodLike(SyntaxNode node) =>
        node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax
        || node is PropertyDeclarationSyntax { ExpressionBody: not null };

    private static int LineCount(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    private static IEnumerable<MemberDeclarationSyntax> TopLevelTypes(SyntaxNode root) =>
        root.ChildNodes()
            .SelectMany<SyntaxNode, SyntaxNode>(n => n is BaseNamespaceDeclarationSyntax ns ? ns.Members : [n])
            .OfType<MemberDeclarationSyntax>()
            .Where(m => m is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

    private static string NameOf(MemberDeclarationSyntax type) => type switch
    {
        BaseTypeDeclarationSyntax t => t.Identifier.Text,
        DelegateDeclarationSyntax d => d.Identifier.Text,
        _ => string.Empty,
    };

    private static IEnumerable<(string Path, SyntaxTree Tree)> Sources()
    {
        var repository = RepositoryRoot();
        return Roots
            .Select(r => Path.Combine(repository, r))
            .Where(Directory.Exists)
            .SelectMany(r => Directory.EnumerateFiles(r, "*.cs", SearchOption.AllDirectories))
            .Where(f => !Path.GetRelativePath(repository, f).Split(Path.DirectorySeparatorChar).Any(SkippedFolders.Contains))
            .Select(f => (Path: Path.GetRelativePath(repository, f), Tree: CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)))
            .Where(s => !IsGenerated(s.Path, s.Tree));
    }

    private static bool IsGenerated(string path, SyntaxTree tree) =>
        path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
        || tree.GetRoot().GetLeadingTrivia().ToFullString().Contains("<auto-generated", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "Filum.slnx")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("Filum.slnx not found above the test output.");
        }

        return folder;
    }
}
