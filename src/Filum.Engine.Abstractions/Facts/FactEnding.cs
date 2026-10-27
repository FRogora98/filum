namespace Filum.Engine.Abstractions.Facts;

/// <summary>How a value of a fact ended (spec 041).</summary>
public static class FactEnding
{
    /// <summary>A new value took its place.</summary>
    public const string Changed = "changed";

    /// <summary>It was wrong: it never held, and a right value took its place.</summary>
    public const string Corrected = "corrected";

    /// <summary>It was withdrawn: it never held, and nothing took its place.</summary>
    public const string Retracted = "retracted";

    /// <summary>It stopped holding, and nothing took its place.</summary>
    public const string Ended = "ended";
}
