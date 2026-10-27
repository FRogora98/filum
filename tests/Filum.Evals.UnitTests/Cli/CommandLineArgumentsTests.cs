using Filum.Evals.Cli;

namespace Filum.Evals.UnitTests.Cli;

public sealed class CommandLineArgumentsTests
{
    [Fact]
    public void Parse_WhenGivenPairsSwitchesAndOtherWords_ShouldKeepThePairsAndTheSwitchesOnly()
    {
        var arguments = CommandLineArguments.Parse(["run", "--Models", "a, b,,c", "--force", "--cap", "0.25", "loose", "--reps"]);

        Assert.Equal(["a", "b", "c"], CommandLineArguments.Split(arguments.Get(OptionNames.Models)!));
        Assert.True(arguments.Has(OptionNames.Force));
        Assert.Equal("true", arguments.Get(OptionNames.Force));
        Assert.Equal(0.25m, arguments.Decimal(OptionNames.Cap, 2m));
        Assert.Equal(2m, arguments.Decimal("missing", 2m));
        Assert.Equal(3, arguments.Int("missing", 3));
        Assert.Throws<FormatException>(() => arguments.Int(OptionNames.Reps, 3));
        Assert.False(arguments.Has("run"));
        Assert.Equal(ResultsFolder.Default, arguments.OutDirectory());
    }
}
