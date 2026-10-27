using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.ClientModel;

namespace Filum.Evals.Judging;

internal static class JudgeModel
{
    public const string Id = "gpt-5.4-mini";
    public const string ApiKeyName = "OPENAI_API_KEY";

    public static ModelPrice Price { get; } = new(0.75m, 4.50m);

    public static ModelClaimJudge Create(string apiKey) =>
        new(new ChatClient(Id, new ApiKeyCredential(apiKey)).AsIChatClient(), Price);
}
