using System.Diagnostics.CodeAnalysis;

namespace Filum.Engine.Packs;

/// <summary>A package read by <see cref="PackLoader.TryLoadAsync"/>: the package, or every problem that refuses it.</summary>
public sealed class PackLoadResult
{
    private PackLoadResult(Pack? pack, IReadOnlyList<string> problems, string? message)
    {
        Pack = pack;
        Problems = problems;
        Message = message;
    }

    /// <summary>The package, when it can be used.</summary>
    public Pack? Pack { get; }

    /// <summary>Every problem found, one sentence each; empty when the package can be used.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>Why the package cannot be used, in the words of <see cref="PackException"/>; null when it can.</summary>
    public string? Message { get; }

    /// <summary>Whether the package can be used.</summary>
    [MemberNotNullWhen(true, nameof(Pack))]
    [MemberNotNullWhen(false, nameof(Message))]
    public bool IsLoaded => Pack is not null;

    internal static PackLoadResult Loaded(Pack pack) => new(pack, [], null);

    internal static PackLoadResult Refused(string folder, IReadOnlyList<string> problems) =>
        new(null, problems, PackException.Describe(folder, problems));
}
