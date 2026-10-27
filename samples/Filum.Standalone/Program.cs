using Filum.Standalone;
using Microsoft.Extensions.AI;

// A console chat with a Filum memory in ./memory. Replace YourModel() with the IChatClient of the model you use:
// the official SDKs of most providers, and local runtimes, offer one (see Microsoft.Extensions.AI).
var agent = new StandaloneAgent(YourModel(), Path.Combine(Environment.CurrentDirectory, "memory"));
Console.WriteLine("Filum standalone: write a message, an empty line to stop.");
while (Console.ReadLine() is { Length: > 0 } line)
{
    Console.WriteLine(await agent.AskAsync(line));
}

static IChatClient YourModel() =>
    throw new NotImplementedException("Plug in your model: return the IChatClient of its SDK here (samples/Filum.Standalone/Program.cs).");
