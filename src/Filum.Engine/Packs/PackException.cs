namespace Filum.Engine.Packs;

/// <summary>A package that cannot be used, with every problem found in it.</summary>
public sealed class PackException(string folder, IReadOnlyList<string> problems) : Exception(Describe(folder, problems))
{
    /// <summary>Every problem found, one sentence each.</summary>
    public IReadOnlyList<string> Problems { get; } = problems;

    internal static string Describe(string folder, IReadOnlyList<string> problems) =>
        $"The package in {folder} cannot be used:\n" + string.Join("\n", problems.Select(p => "- " + p));
}
