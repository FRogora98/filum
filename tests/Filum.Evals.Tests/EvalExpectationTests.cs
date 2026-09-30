
using Filum.Evals;

namespace Filum.Evals.Tests;

/// <summary>The eval's checks, on synthetic turns and memories: every expectation type passes and fails.</summary>
public sealed class EvalExpectationTests
{
    private static readonly StepDto Wrote = new(StepDto.Wrote, "memory_append", "/lists/films.csv", "Added 1 row", 10, 7);
    private static readonly StepDto Computed = new(StepDto.Computed, "collection_aggregate", "/lists/films.csv", "Computed", 10, null);

    private static TurnRecord Turn(string answer, params StepDto[] steps) => new(1, "a", "message", answer, steps, 100, 10, 0.0001m, 1.2);

    private static MemoryFileDetailDto File(string path, string content, string sensitivity = "normal") =>
        new(path, path.EndsWith(".csv") ? "collection" : "document", content.Length, content.Split('\n').Length, sensitivity, DateTimeOffset.UtcNow, null, null, content,
            new MemoryOriginDto("agent", null, DateTimeOffset.UtcNow));

    private static readonly MemorySnapshot Memory = new([
        File("/filum.md", "# About you\nFavourite colour: teal.\n# Rules\n- Keep answers short.\n"),
        File("/lists/films-to-watch.csv", "title,year\nDune,2021\n\"Arrival, the film\",2016\n"),
        File("/notes/diary.md", "A quiet day.\n", "private")
    ]);

    private static bool Passes(string json, TurnRecord turn) =>
        Expectations.Check(ScenarioLoader.Parse($$"""{"id":"t","language":"en","turns":[{"chat":"a","message":"m","expect":[{{json}}]}]}""", "test").Turns[0].Expect[0], turn, Memory).Passed;

    [Theory]
    [InlineData("""{"type":"wrote"}""", true, false)]
    [InlineData("""{"type":"no_write"}""", false, true)]
    [InlineData("""{"type":"step","kind":"computed"}""", true, false)]
    public void Turn_expectations_read_the_steps(string json, bool withSteps, bool withoutSteps)
    {
        Assert.Equal(withSteps, Passes(json, Turn("ok", Wrote, Computed)));
        Assert.Equal(withoutSteps, Passes(json, Turn("ok")));
    }

    [Theory]
    [InlineData("You have 2,396 pages in total.", true)]
    [InlineData("You have 2.396 pages in total.", true)]
    [InlineData("You have 2396 pages in total.", true)]
    [InlineData("You have 2 396 pages.", true)]
    [InlineData("You have 23,96 pages.", false)]
    [InlineData("About two thousand.", false)]
    public void Numbers_match_with_or_without_thousand_separators(string answer, bool passes) =>
        Assert.Equal(passes, Passes("""{"type":"answer_contains","any":["2396"]}""", Turn(answer)));

    [Fact]
    public void Answer_expectations_accept_alternatives_ignore_case_and_count_words()
    {
        Assert.True(Passes("""{"type":"answer_contains","any":["teal","verde acqua"]}""", Turn("Your favourite colour is Teal.")));
        Assert.False(Passes("""{"type":"answer_not_contains","any":["teal"]}""", Turn("It is TEAL.")));
        Assert.True(Passes("""{"type":"answer_max_words","max":5}""", Turn("Teal, as you said.")));
        Assert.False(Passes("""{"type":"answer_max_words","max":3}""", Turn("Teal, as you said.")));
    }

    [Fact]
    public void Core_and_memory_expectations_read_the_files()
    {
        Assert.True(Passes("""{"type":"core_contains","any":["short","breve"]}""", Turn("ok")));
        Assert.False(Passes("""{"type":"core_not_contains","any":["short"]}""", Turn("ok")));
        Assert.True(Passes("""{"type":"memory_contains","any":["quiet day"]}""", Turn("ok")));
    }

    [Fact]
    public void Files_are_found_by_kind_by_any_word_of_their_name_and_by_sensitivity()
    {
        Assert.True(Passes("""{"type":"file","kind":"collection","name_any":["movie","film"]}""", Turn("ok")));
        Assert.False(Passes("""{"type":"file","kind":"document","name_any":["film"]}""", Turn("ok")));
        Assert.True(Passes("""{"type":"file","name_any":["diary","journal"],"sensitivity":"private"}""", Turn("ok")));
        Assert.False(Passes("""{"type":"file","name_any":["films"],"sensitivity":"private"}""", Turn("ok")));
    }

    [Fact]
    public void Rows_are_counted_and_searched_in_every_field()
    {
        Assert.True(Passes("""{"type":"rows","name_any":["film"],"count":2,"contain":["dune","arrival"]}""", Turn("ok")));
        Assert.False(Passes("""{"type":"rows","name_any":["film"],"count":3}""", Turn("ok")));
        Assert.False(Passes("""{"type":"rows","name_any":["film"],"contain":["Past Lives"]}""", Turn("ok")));
        Assert.False(Passes("""{"type":"rows","name_any":["books"]}""", Turn("ok")));
    }

    [Fact]
    public void A_word_in_a_folder_of_the_path_finds_the_file_too()
    {
        var memory = new MemorySnapshot([File("/shopping/list.csv", "item\nmilk\neggs\n")]);
        var rows = ScenarioLoader.Parse("""{"id":"t","language":"en","turns":[{"chat":"a","message":"m","expect":[{"type":"rows","name_any":["shopping"],"count":2}]}]}""", "t").Turns[0].Expect[0];

        Assert.True(Expectations.Check(rows, Turn("ok"), memory).Passed);
    }

    [Fact]
    public void Typographic_apostrophes_read_as_plain_ones()
    {
        Assert.True(Passes("""{"type":"answer_contains","any":["don't know"]}""", Turn("I don’t know your sister’s name.")));
    }

    [Fact]
    public void A_failed_turn_fails_every_expectation()
    {
        var failed = new TurnRecord(1, "a", "m", string.Empty, [], 0, 0, 0, 0.1, "502 the model failed");

        Assert.False(Passes("""{"type":"no_write"}""", failed));
    }

    [Fact]
    public void An_unknown_expectation_type_is_a_clear_error()
    {
        var error = Assert.Throws<InvalidDataException>(() => ScenarioLoader.Parse("""{"id":"x","language":"en","turns":[{"chat":"a","message":"m","expect":[{"type":"magic"}]}]}""", "x.json"));

        Assert.Contains("unknown expectation type 'magic'", error.Message);
    }
    [Fact]
    public void Skill_expectations_read_the_skills_the_proposals_and_the_steps()
    {
        var starter = Skills.Starters[0];
        var own = new Skill("log-note", "d", "w", false, "s");
        var memory = new MemorySnapshot([
            File(Skills.PathOf(starter.Name), Skills.Format(starter)),
            File("/skills/log-note.md", Skills.Format(own))
        ]);
        var onlyStarters = new MemorySnapshot([memory.Files[0]]);
        bool Check(string json, TurnRecord turn, MemorySnapshot m, IReadOnlyList<TurnRecord>? earlier = null) =>
            Expectations.Check(ScenarioLoader.Parse($$"""{"id":"t","language":"en","turns":[{"chat":"a","message":"m","expect":[{{json}}]}]}""", "test").Turns[0].Expect[0], turn, m, earlier).Passed;

        Assert.True(Check("""{"type":"skill","name_any":["log-note"],"enabled":false}""", Turn("ok"), memory));
        Assert.False(Check("""{"type":"skill","name_any":["log-note"],"enabled":true}""", Turn("ok"), memory));
        Assert.False(Check("""{"type":"skill"}""", Turn("ok"), onlyStarters));
        Assert.True(Check("""{"type":"no_skill"}""", Turn("ok"), onlyStarters));
        Assert.False(Check("""{"type":"no_skill"}""", Turn("ok"), memory));

        var used = new StepDto(StepDto.Read, "skill", "/skills/log-note.md", "Used skill /log-note", 0, null);
        Assert.True(Check("""{"type":"used_skill"}""", Turn("ok", used), memory));
        Assert.False(Check("""{"type":"used_skill"}""", Turn("ok", Wrote), memory));

        var proposed = Turn("Save it?") with { Proposal = new SkillProposalDto("hardware-list", "d", "w", "s") };
        Assert.True(Check("""{"type":"proposal"}""", proposed, memory));
        Assert.True(Check("""{"type":"proposal","name_any":["hardware"]}""", Turn("ok"), memory, [proposed]));
        Assert.False(Check("""{"type":"proposal"}""", Turn("ok"), memory, [Turn("ok")]));

        Assert.True(Passes("""{"type":"file","name_any":["films"]}""", Turn("ok")));
        Assert.False(Check("""{"type":"file","name_any":["log-note"]}""", Turn("ok"), memory));
    }
}
