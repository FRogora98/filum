using Filum.Engine.Packs;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// The person's core and what the agent learned, created the first time, and the package every new memory starts from.
/// A role of <see cref="IMemoryService"/>, for callers that need only this.
/// </summary>
public interface IMemoryCore
{
    /// <summary>The package every new memory starts from, or null for the generic engine.</summary>
    Pack? Pack { get; }

    /// <summary>The person's core, created the first time with the starter skills and, with a package, its core, files and skills.</summary>
    Task<string> EnsureCoreAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>What the agent learned about the person, created empty the first time; its lines, as the instructions carry them.</summary>
    Task<string> EnsureLearnedAsync(Guid userId, CancellationToken cancellationToken);
}
