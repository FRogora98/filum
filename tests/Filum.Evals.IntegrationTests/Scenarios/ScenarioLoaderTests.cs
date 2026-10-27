using Filum.Evals.Scenarios;

namespace Filum.Evals.IntegrationTests.Scenarios;

public sealed class ScenarioLoaderTests
{
    [Fact]
    public void LoadAll_WhenGivenAFolderOutsideTheRepository_ShouldReadAndFilterItsScenarios()
    {
        var folder = Path.Combine(Path.GetTempPath(), "filum-evals-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "one.json"), """{"id":"guard-one","language":"it","turns":[{"chat":"a","message":"Ciao","expect":[{"type":"judge","question":"Is the reply in Italian?","pass_if":"yes"}]}]}""");
            File.WriteAllText(Path.Combine(folder, "two.json"), """{"id":"other","language":"en","turns":[{"chat":"a","message":"Hi","expect":[]}]}""");

            Assert.Equal(["guard-one", "other"], ScenarioLoader.LoadAll(folder).Select(s => s.Id));
            Assert.Equal(["guard-one"], ScenarioLoader.LoadAll(folder, "guard-*").Select(s => s.Id));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
