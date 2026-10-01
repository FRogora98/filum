namespace Filum.Evals.LongMemEval;

/// <summary>Asks a fixed model a yes/no prompt as it is, and reads the answer as LongMemEval does ("yes" anywhere).</summary>
public interface IPromptJudge
{
    Task<(bool Yes, decimal CostUsd)> YesAsync(string prompt, CancellationToken cancellationToken);
}

/// <summary>
/// LongMemEval's own judge prompts, one per ability, verbatim from the authors' evaluate_qa.py
/// (LongMemEval, MIT License, Copyright (c) 2024 Di Wu, https://github.com/xiaowu0162/LongMemEval).
/// </summary>
public static class LmeJudge
{
    private const string Contains =
        "I will give you a question, a correct answer, and a response from a model. Please answer yes if the response contains the correct answer. Otherwise, answer no. If the response is equivalent to the correct answer or contains all the intermediate steps to get the correct answer, you should also answer yes. If the response only contains a subset of the information required by the answer, answer no. \n\nQuestion: {0}\n\nCorrect Answer: {1}\n\nModel Response: {2}\n\nIs the model response correct? Answer yes or no only.";

    private const string Temporal =
        "I will give you a question, a correct answer, and a response from a model. Please answer yes if the response contains the correct answer. Otherwise, answer no. If the response is equivalent to the correct answer or contains all the intermediate steps to get the correct answer, you should also answer yes. If the response only contains a subset of the information required by the answer, answer no. In addition, do not penalize off-by-one errors for the number of days. If the question asks for the number of days/weeks/months, etc., and the model makes off-by-one errors (e.g., predicting 19 days when the answer is 18), the model's response is still correct. \n\nQuestion: {0}\n\nCorrect Answer: {1}\n\nModel Response: {2}\n\nIs the model response correct? Answer yes or no only.";

    private const string Update =
        "I will give you a question, a correct answer, and a response from a model. Please answer yes if the response contains the correct answer. Otherwise, answer no. If the response contains some previous information along with an updated answer, the response should be considered as correct as long as the updated answer is the required answer.\n\nQuestion: {0}\n\nCorrect Answer: {1}\n\nModel Response: {2}\n\nIs the model response correct? Answer yes or no only.";

    private const string Preference =
        "I will give you a question, a rubric for desired personalized response, and a response from a model. Please answer yes if the response satisfies the desired response. Otherwise, answer no. The model does not need to reflect all the points in the rubric. The response is correct as long as it recalls and utilizes the user's personal information correctly.\n\nQuestion: {0}\n\nRubric: {1}\n\nModel Response: {2}\n\nIs the model response correct? Answer yes or no only.";

    private const string Abstention =
        "I will give you an unanswerable question, an explanation, and a response from a model. Please answer yes if the model correctly identifies the question as unanswerable. The model could say that the information is incomplete, or some other information is given but the asked information is not.\n\nQuestion: {0}\n\nExplanation: {1}\n\nModel Response: {2}\n\nDoes the model correctly identify the question as unanswerable? Answer yes or no only.";

    public static string Prompt(LmeInstance instance, string response)
    {
        var template = instance.IsAbstention ? Abstention : instance.QuestionType switch
        {
            "single-session-user" or "single-session-assistant" or "multi-session" => Contains,
            "temporal-reasoning" => Temporal,
            "knowledge-update" => Update,
            "single-session-preference" => Preference,
            _ => throw new NotSupportedException($"LongMemEval has no judge prompt for '{instance.QuestionType}'.")
        };

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, template, instance.Question, instance.AnswerText, response);
    }
}
