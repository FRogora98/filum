namespace Filum.Evals.Hosts.Wire;

internal sealed record SendMessageResponse(ConversationDto Conversation, MessageDto UserMessage, MessageDto AssistantMessage, UsageDto? Usage);
