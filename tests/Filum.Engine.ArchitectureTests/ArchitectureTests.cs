using System.Reflection;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Storage.LocalFolder;
using NetArchTest.Rules;

namespace Filum.Engine.ArchitectureTests;

public sealed class ArchitectureTests
{
    private static readonly Assembly Abstractions = typeof(IMemoryStore).Assembly;
    private static readonly Assembly Engine = typeof(IMemoryService).Assembly;
    private static readonly Assembly LocalFolder = typeof(LocalFolderStore).Assembly;
    private static readonly Assembly InMemory = typeof(InMemoryMemoryStore).Assembly;
    private static readonly Assembly Embeddings = typeof(Filum.Embeddings.E5Embedder).Assembly;
    private static readonly Assembly[] Libraries = [Abstractions, Engine, LocalFolder, InMemory, Embeddings];

    private const string InterpreterNamespace = "Filum.Engine.Collections.Compute.Jint";
    private const string PackFolderNamespace = "Filum.Engine.Packs.FileSystem";
    // CI collects coverage: Coverlet adds a tracker type, which writes its hits to a file, to every assembly it instruments.
    private const string CoverageNamespace = "Coverlet";

    // Files and folders, read or written: System.IO.File… covers File, FileInfo and FileStream.
    private static readonly string[] FileSystemApis = ["System.IO.File", "System.IO.Directory", "System.IO.DriveInfo"];

    private static readonly string[] Hosts =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "OpenAI",
        "ModelContextProtocol",
        "Microsoft.Extensions.Hosting",
    ];

    // A new public type is added here on purpose (SDK-01).
    private static readonly string[] EnginePublicTypes =
    [
        "Filum.Engine.FilumEngine",
        "Filum.Engine.Collections.Csv",
        "Filum.Engine.Consolidation.ConsolidationPrompt",
        "Filum.Engine.DependencyInjection.FilumEngineBuilder",
        "Filum.Engine.DependencyInjection.FilumEngineServiceCollectionExtensions",
        "Filum.Engine.Facts.FactTable",
        "Filum.Engine.Imports.Importer",
        "Filum.Engine.Instructions.InstructionParts",
        "Filum.Engine.Instructions.PlatformInstructions",
        "Filum.Engine.Memory.IMemoryService",
        "Filum.Engine.Memory.MemoryPaths",
        "Filum.Engine.Memory.Contracts.IMemoryConsolidation",
        "Filum.Engine.Memory.Contracts.IMemoryCore",
        "Filum.Engine.Memory.Contracts.IMemoryEvents",
        "Filum.Engine.Memory.Contracts.IMemoryFacts",
        "Filum.Engine.Memory.Contracts.IMemoryBriefing",
        "Filum.Engine.Memory.Contracts.IMemoryForgetting",
        "Filum.Engine.Memory.Contracts.IMemoryHistory",
        "Filum.Engine.Memory.Contracts.IMemoryImports",
        "Filum.Engine.Memory.Contracts.IMemoryReader",
        "Filum.Engine.Memory.Contracts.IMemorySkills",
        "Filum.Engine.Memory.Contracts.IMemoryWriter",
        "Filum.Engine.Memory.Core.LearnedFile",
        "Filum.Engine.Memory.Files.MemoryText",
        "Filum.Engine.Packs.Pack",
        "Filum.Engine.Packs.PackException",
        "Filum.Engine.Packs.PackFile",
        "Filum.Engine.Packs.PackLoader",
        "Filum.Engine.Packs.PackLoadResult",
        "Filum.Engine.Skills.SkillFormat",
        "Filum.Engine.Skills.SkillInvocation",
        "Filum.Engine.Skills.StarterSkills",
        "Filum.Engine.Tools.IMemoryToolsFactory",
        "Filum.Engine.Tools.MemoryTools",
    ];

    [Fact]
    public void Libraries_WhenBuilt_ShouldNotDependOnAnyHost()
    {
        // SOL-03, SDK-06: the libraries are usable from a console, with no web, database, model provider or MCP host.
        foreach (var assembly in Libraries)
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(Hosts).GetResult();
            Assert.True(result.IsSuccessful, Failing(result));
        }
    }

    [Fact]
    public void Libraries_WhenBuilt_ShouldNotReadConfiguration()
    {
        // CFG-02: options come in as IOptions<T>; IConfiguration belongs to the host's composition.
        foreach (var assembly in Libraries)
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOn("Microsoft.Extensions.Configuration").GetResult();
            Assert.True(result.IsSuccessful, Failing(result));
        }
    }

    [Fact]
    public void Classes_WhenConcrete_ShouldBeSealed()
    {
        // CS-04: classes are sealed unless inheritance is intended.
        foreach (var assembly in Libraries.Append(typeof(Filum.Mcp.ModelSlot).Assembly))
        {
            // Internal classes too: an app such as filum-mcp is all internal.
            var open = Types.InAssembly(assembly).That().AreClasses().And().AreNotAbstract().GetTypes()
                .Where(t => !t.IsSealed && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) && !t.Name.Contains('<', StringComparison.Ordinal))
                .Select(t => t.FullName);
            Assert.Empty(open);
        }
    }

    [Fact]
    public void Abstractions_WhenBuilt_ShouldReferenceOnlyTheRuntime()
    {
        // ARC-01, SDK-03: the ports and models need no implementation, no interpreter and no package; a store for
        // another database depends on them alone.
        Assert.DoesNotContain(Abstractions.GetReferencedAssemblies().Select(a => a.Name!), n => !IsRuntime(n));
    }

    [Fact]
    public void Engine_WhenBuilt_ShouldReferenceNoStorageAndNoOtherLibraryOfTheEngine()
    {
        // ARC-01, ARC-02: Abstractions <- Engine <- Storage.*; the engine knows the stores only through their ports.
        var filum = Engine.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Filum.", StringComparison.Ordinal));
        Assert.Equal(["Filum.Engine.Abstractions"], filum);
    }

    [Fact]
    public void Storages_WhenBuilt_ShouldReferenceOnlyTheEngineAndItsAbstractions()
    {
        // ARC-02: a storage package extends the engine's builder and implements its ports, nothing more.
        foreach (var storage in new[] { LocalFolder, InMemory })
        {
            var filum = storage.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Filum.", StringComparison.Ordinal)).Order();
            Assert.Equal(["Filum.Engine", "Filum.Engine.Abstractions"], filum);
        }
    }

    [Fact]
    public void Engine_WhenBuilt_ShouldContainNoStorageImplementation()
    {
        // ARC-01, ARC-02, SDK-03: the stores live in Filum.Engine.Storage.*; no type of the engine implements the
        // store, and none it exposes implements one of its roles.
        Assert.Empty(Types.InAssembly(Engine).That().ImplementInterface(typeof(IMemoryStore)).GetTypes());
        Type[] roles = [typeof(IMemoryFileStore), typeof(IMemoryRevisionStore), typeof(IMemoryEventStore), typeof(IMemoryVectorStore), typeof(IConversationDirectory)];
        Assert.DoesNotContain(Engine.GetExportedTypes(), t => roles.Any(r => r.IsAssignableFrom(t)));
    }

    [Fact]
    public void Engine_WhenBuilt_ShouldExposeOnlyItsEntryPointsAndModels()
    {
        // SDK-01: everything else is internal, the services and the helpers first of all.
        Assert.Equal(EnginePublicTypes.Order(StringComparer.Ordinal), Engine.GetExportedTypes().Select(t => t.FullName!).Order(StringComparer.Ordinal));
        var helpers = new[] { "CollectionAggregator", "JintCodeSandbox", "TrendCalculator", "LineChanges", "MemoryVectors", "EventSearch", "MemoryService", "MemoryWriter" };
        var result = Types.InAssembly(Engine).That().HaveName(helpers).ShouldNot().BePublic().GetResult();
        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Engine_WhenBuilt_ShouldReachTheInterpreterOnlyThroughItsSandbox()
    {
        // ARC-01, ARC-03 (approved exception until the next major, docs/EXCEPTIONS.md): Jint stays in the engine, but
        // only in Collections/Compute/Jint; everything else runs code through ICodeSandbox.
        var result = Types.InAssembly(Engine).That().DoNotResideInNamespace(InterpreterNamespace).ShouldNot().HaveDependencyOn("Jint").GetResult();
        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Libraries_WhenBuilt_ShouldTouchFilesOnlyWhereThatIsTheirJob()
    {
        // ARC-03: the engine reads a package's folder only in Packs/FileSystem (behind IPackSource, an approved exception
        // until the next major); the abstractions and the in-memory store touch no file. The local folder store, the
        // embedding model's files, filum-mcp and the evals work with files by design.
        var engine = Types.InAssembly(Engine).That().DoNotResideInNamespace(PackFolderNamespace).And().DoNotResideInNamespace(CoverageNamespace).ShouldNot().HaveDependencyOnAny(FileSystemApis).GetResult();
        Assert.True(engine.IsSuccessful, Failing(engine));
        foreach (var assembly in new[] { Abstractions, InMemory })
        {
            var result = Types.InAssembly(assembly).That().DoNotResideInNamespace(CoverageNamespace).ShouldNot().HaveDependencyOnAny(FileSystemApis).GetResult();
            Assert.True(result.IsSuccessful, Failing(result));
        }
    }

    private static bool IsRuntime(string assembly) => assembly is "System.Runtime" or "netstandard" || assembly.StartsWith("System.", StringComparison.Ordinal);

    private static string Failing(TestResult result) => string.Join(", ", result.FailingTypeNames ?? []);
}
