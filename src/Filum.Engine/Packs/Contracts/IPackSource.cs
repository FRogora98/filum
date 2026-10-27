namespace Filum.Engine.Packs.Contracts;

/// <summary>Where the files of a package are read from, before the engine checks them.</summary>
internal interface IPackSource
{
    /// <summary>The files of the package at <paramref name="location"/>, or null when there is no package there.</summary>
    PackContent? Read(string location);
}
