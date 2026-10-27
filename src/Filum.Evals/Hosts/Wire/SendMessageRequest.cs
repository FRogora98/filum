namespace Filum.Evals.Hosts.Wire;

internal sealed record SendMessageRequest(Guid Id, string? Content, string? Model = null);
