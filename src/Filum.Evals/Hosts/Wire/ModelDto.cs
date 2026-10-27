namespace Filum.Evals.Hosts.Wire;

internal sealed record ModelDto(string Id, string Name, string Description, decimal InputPricePerMillionUsd, decimal OutputPricePerMillionUsd, bool IsDefault);
