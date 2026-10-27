namespace Filum.Evals.Hosts.Wire;

internal sealed record ConversationDto(Guid Id, string Title, string Model, string Preview, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
