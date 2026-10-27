using System.Diagnostics;
using System.Globalization;
using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Collections.Compute.Jint;

public sealed class JintCodeSandboxTests
{
    private readonly JintCodeSandbox _sandbox = new();

    [Theory]
    [InlineData("while (true) {}", "stopped")]
    [InlineData("const a = []; while (true) { a.push(new Array(100000).fill(1)); }", "stopped")]
    [InlineData("function f(n) { return f(n + 1); } return f(0);", "recursed")]
    public void Run_WhenTheCodeDoesNotEnd_ShouldStopItAtItsLimit(string code, string reason)
    {
        var clock = Stopwatch.StartNew();

        var result = _sandbox.Run(code, new Dictionary<string, CsvTable>());

        Assert.Null(result.Json);
        Assert.Contains(reason, result.Error);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), clock.Elapsed.ToString());
    }

    [Fact]
    public void Run_WhenARegularExpressionBacktracks_ShouldStopItAtTheTimeLimit()
    {
        var clock = Stopwatch.StartNew();

        var result = _sandbox.Run("return /^(a+)+$/.test('a'.repeat(40) + 'b');", new Dictionary<string, CsvTable>());

        Assert.Null(result.Json);
        Assert.NotNull(result.Error);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), clock.Elapsed.ToString());
    }

    [Fact]
    public void Run_WhenCancelled_ShouldStop()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var result = _sandbox.Run("let i = 0; while (i < 100) { i++; } return i;", new Dictionary<string, CsvTable>(), cancelled.Token);

        Assert.Null(result.Json);
        Assert.Contains("cancelled", result.Error);
    }

    [Fact]
    public void Run_WhenTheCodeFails_ShouldKeepWhatWasPrintedWithinTheLimit()
    {
        var result = _sandbox.Run("print('x'.repeat(100000)); print('y'.repeat(100000)); throw new Error('stop');", new Dictionary<string, CsvTable>());

        Assert.StartsWith("The code failed", result.Error);
        Assert.True(result.Output.Length <= JintCodeSandbox.MaxOutputChars, result.Output.Length.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("return System.IO.File.ReadAllText('x');")]
    [InlineData("return require('fs');")]
    [InlineData("return fetch('https://example.invalid');")]
    [InlineData("return importScripts('x');")]
    public void Run_WhenTheCodeReachesOutsideTheInterpreter_ShouldFail(string code)
    {
        var result = _sandbox.Run(code, new Dictionary<string, CsvTable>());

        Assert.Null(result.Json);
        Assert.StartsWith("The code failed", result.Error);
    }
}
