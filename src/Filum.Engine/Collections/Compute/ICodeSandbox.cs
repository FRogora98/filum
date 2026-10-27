using Filum.Engine.Abstractions.Collections;

namespace Filum.Engine.Collections.Compute;

/// <summary>
/// Runs the agent's own code on collections, isolated: the code reaches nothing but the rows it is given, its time and
/// memory are bounded, and a run returns values and writes nothing.
/// </summary>
internal interface ICodeSandbox
{
    ComputeResult Run(string code, IReadOnlyDictionary<string, CsvTable> collections, CancellationToken cancellationToken = default);
}
