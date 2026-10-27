using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Facts;

namespace Filum.Engine.Testing;

public abstract partial class MemoryServiceContract
{
    [Fact]
    public async Task BuildIndexAsync_WhenFilesExist_ShouldListEveryNonPrivateFileNewestFirstAndNeverTheCore()
    {
        await _memory.EnsureCoreAsync(_user, None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/old.md", "one\ntwo\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/lists/films.csv", "title,year\nDune,2021\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/secret.md", "hidden\n", None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/notes/secret.md", MemorySensitivity.Private, None));

        var lines = (await _memory.BuildIndexAsync(_user, None)).Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("- /lists/films.csv (collection · fields: title, year · 1 rows · changed ", lines[0]);
        Assert.StartsWith("- /notes/old.md (document · 2 lines · changed ", lines[1]);
        Assert.Equal("(nothing yet besides the core)", await _memory.BuildIndexAsync(Guid.NewGuid(), None));
    }

    [Fact]
    public async Task BuildIndexAsync_WhenFilesHoldContent_ShouldMapWhatEachHoldsAndKeepNoMapInTheCore()
    {
        var core = await _memory.EnsureCoreAsync(_user, None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/trip.md", "# The trip\n\nPlans for the walk along the river in May.\nDay one.\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/empty.md", "# Only a title\n", None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "3", null, null, [], None));

        var lines = (await _memory.BuildIndexAsync(_user, None)).Split('\n');

        Assert.DoesNotContain("map", core, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith($"- {FactTable.Path} (facts that hold for a time · 1 rows", lines[0]);
        Assert.EndsWith("): Only a title", lines[1]);
        Assert.EndsWith("): Plans for the walk along the river in May.", lines[2]);
    }

    [Fact]
    public async Task BuildIndexAsync_WhenPastItsLimit_ShouldListTheNewestFilesAndSayHowManyAreLeftOut()
    {
        var memory = Memory(new MemoryOptions { IndexMaxFiles = 2 });
        foreach (var name in new[] { "a", "b", "c", "d" })
        {
            Ok(await memory.WriteAsync(_user, Agent, $"/notes/{name}.md", name, None));
        }

        var lines = (await memory.BuildIndexAsync(_user, None)).Split('\n');

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("- /notes/d.md", lines[0]);
        Assert.StartsWith("- /notes/c.md", lines[1]);
        Assert.Equal("- …and 2 older files not listed here: memory_search finds them.", lines[2]);
    }
}
