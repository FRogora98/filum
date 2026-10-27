using Filum.Evals.LongMemEval;

namespace Filum.Evals.UnitTests.Builders;

internal static class LongMemEvalInstances
{
    private const string File3 = """
        [
          {"question_id": "t1", "question_type": "temporal-reasoning", "question": "Which came first, the bike or the boat?", "answer": "The bike",
           "question_date": "2023/05/01 (Mon) 10:00", "haystack_session_ids": ["b", "a"], "haystack_dates": ["2023/04/10 (Mon) 17:50", "2023/04/10 (Mon) 14:47"],
           "haystack_sessions": [[{"role": "user", "content": "I bought a boat today."}], [{"role": "user", "content": "I bought a bike today.", "has_answer": true}, {"role": "assistant", "content": "Nice!"}]],
           "answer_session_ids": ["a"]},
          {"question_id": "u1_abs", "question_type": "single-session-user", "question": "What is my cat's name?", "answer": "You never said.",
           "question_date": "2023/05/02 (Tue) 09:00", "haystack_session_ids": ["c"], "haystack_dates": ["2023/04/01 (Sat) 08:00"],
           "haystack_sessions": [[{"role": "user", "content": "I have a dog named Rex."}]], "answer_session_ids": []},
          {"question_id": "k1", "question_type": "knowledge-update", "question": "How many plants do I have now?", "answer": 4,
           "question_date": "2023/06/01 (Thu) 09:00", "haystack_session_ids": ["d", "e"], "haystack_dates": ["2023/04/01 (Sat) 08:00", "2023/05/01 (Mon) 08:00"],
           "haystack_sessions": [[{"role": "user", "content": "I have 3 plants."}], [{"role": "user", "content": "I bought another plant, so 4 now."}]], "answer_session_ids": ["e"]}
        ]
        """;

    public static async Task<IReadOnlyList<LongMemEvalInstance>> LoadAsync()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(File3));
        return await LongMemEvalDataset.LoadAsync(stream);
    }
}
