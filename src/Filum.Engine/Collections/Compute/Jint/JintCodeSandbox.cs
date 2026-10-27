using System.Globalization;
using System.Text.Json;
using System.Text;
using Filum.Engine.Abstractions.Collections;
using Jint.Native;
using Jint.Runtime;
using Jint;
using JintEngine = Jint.Engine;

namespace Filum.Engine.Collections.Compute.Jint;

// Isolated: nothing reachable but the rows it is given (no .NET, network or files); time, memory, statements and
// recursion are bounded.
internal sealed class JintCodeSandbox : ICodeSandbox
{
    public static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(2);
    public const long MemoryLimit = 64L * 1024 * 1024;
    public const int MaxStatements = 1_000_000;
    public const int MaxRecursion = 100;
    public const int MaxRows = 20_000;
    public const int MaxResultBytes = 8 * 1024;
    public const int MaxOutputChars = 4 * 1024;

    private const int BytesPerKilobyte = 1024;

    // The names the code sees: its rows arrive as JSON in DataVariable and are parsed into `data`; the result is read
    // back through ResultVariable; print writes to the run's output.
    private const string DataVariable = "__data";
    private const string ResultVariable = "__result";
    private const string PrintFunction = "print";
    private const string Prelude = $"const data = JSON.parse({DataVariable}); {DataVariable} = undefined;";
    private const string ResultAsJson = $"JSON.stringify({ResultVariable} === undefined ? null : {ResultVariable})";

    public ComputeResult Run(string code, IReadOnlyDictionary<string, CsvTable> collections, CancellationToken cancellationToken = default)
    {
        var rows = collections.Values.Sum(t => t.Rows.Count);
        if (rows > MaxRows)
        {
            return new ComputeResult(null, string.Empty, $"The collections have {rows} rows, over the limit of {MaxRows}; name fewer.", rows);
        }

        var output = new StringBuilder();
        using var engine = Sandbox(output, cancellationToken);
        engine.SetValue(DataVariable, Json(collections));
        try
        {
            engine.Execute(Prelude);
            var result = engine.Evaluate($"(function () {{\n{code}\n}})()");
            engine.SetValue(ResultVariable, result);
            var json = engine.Evaluate(ResultAsJson).AsString();
            return Encoding.UTF8.GetByteCount(json) > MaxResultBytes
                ? new ComputeResult(null, output.ToString(), $"The result is over {MaxResultBytes / BytesPerKilobyte} KB; return less (a summary, the top rows).", rows)
                : new ComputeResult(json, output.ToString(), null, rows);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new ComputeResult(null, output.ToString(), Reason(exception), rows);
        }
    }

    private static JintEngine Sandbox(StringBuilder output, CancellationToken cancellationToken)
    {
        var engine = new JintEngine(options =>
        {
            options
                .Strict()
                .TimeoutInterval(TimeLimit)
                .LimitMemory(MemoryLimit)
                .MaxStatements(MaxStatements)
                .LimitRecursion(MaxRecursion)
                .CancellationToken(cancellationToken);
            options.Constraints.RegexTimeout = TimeLimit;
        });
        engine.SetValue(PrintFunction, new Action<JsValue>(value =>
        {
            var text = (value.IsString() ? value.AsString() : value.ToString()) + "\n";
            output.Append(text, 0, Math.Min(text.Length, MaxOutputChars - output.Length));
        }));
        return engine;
    }

    // In words the agent can act on.
    private static string Reason(Exception exception) => exception switch
    {
        ExecutionCanceledException => "The run was cancelled.",
        TimeoutException => $"The code ran over {TimeLimit.TotalSeconds:0} seconds and was stopped.",
        MemoryLimitExceededException => $"The code used over {MemoryLimit / BytesPerKilobyte / BytesPerKilobyte} MB and was stopped.",
        StatementsCountOverflowException => $"The code ran over {MaxStatements:N0} statements and was stopped.",
        RecursionDepthOverflowException => $"The code recursed over {MaxRecursion} levels and was stopped.",
        JavaScriptException javaScript => $"The code failed: {javaScript.Error}",
        _ => $"The code could not run: {exception.Message}"
    };

    // A value that reads as a number is a number.
    private static string Json(IReadOnlyDictionary<string, CsvTable> collections)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (path, table) in collections)
            {
                writer.WriteStartArray(path);
                foreach (var row in table.Rows)
                {
                    WriteRow(writer, table.Header, row);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteRow(Utf8JsonWriter writer, IReadOnlyList<string> header, CsvRecord row)
    {
        writer.WriteStartObject();
        for (var i = 0; i < header.Count; i++)
        {
            var value = row.Fields[i];
            if (double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
            {
                writer.WriteNumber(header[i], number);
            }
            else
            {
                writer.WriteString(header[i], value);
            }
        }

        writer.WriteEndObject();
    }
}
