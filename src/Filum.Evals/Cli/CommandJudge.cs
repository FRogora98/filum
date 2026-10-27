using Filum.Evals.Judging;

namespace Filum.Evals.Cli;

internal static class CommandJudge
{
    public static ModelClaimJudge Create(DotEnvSecrets secrets) =>
        JudgeModel.Create(secrets.Find(JudgeModel.ApiKeyName) ?? throw new InvalidOperationException($"{JudgeModel.ApiKeyName} is needed for the judge: set it in the environment or in a .env file."));
}
