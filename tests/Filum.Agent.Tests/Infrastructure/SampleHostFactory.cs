using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;

namespace Filum.Agent.Tests.Infrastructure;

/// <summary>
/// The sample host over the shared PostgreSQL, talking to a <see cref="FakeChatClient"/>. Each instance is a separate
/// host; <paramref name="services"/> adds a test's own tools, gates or observers.
/// </summary>
public sealed class SampleHostFactory(
    PostgresFixture postgres, FakeChatClient chatClient, IReadOnlyDictionary<string, string>? settings = null, Action<IServiceCollection>? services = null)
    : WebApplicationFactory<Program>
{
    public FakeChatClientProvider Models { get; } = new(chatClient);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:sample", postgres.ConnectionString);
        builder.UseSetting("Providers:openai:ApiKey", "test-key");
        builder.UseSetting("Memory:PackPath", Path.Combine(Root(), "packs", "example"));
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(collection =>
        {
            collection.RemoveAll<IChatClientProvider>();
            collection.AddSingleton<IChatClientProvider>(Models);
            services?.Invoke(collection);
        });
    }

    /// <summary>A client that is this person, the sample's way (a header, sample only).</summary>
    public HttpClient Person(Guid person)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Sample-Person", person.ToString());
        return client;
    }

    public static string Root()
    {
        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "Filum.slnx")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("Filum.slnx not found above the test output.");
        }

        return folder;
    }
}

public static class SampleHostClient
{
    public static Task<HttpResponseMessage> Send(this HttpClient client, Guid conversation, string content, string? model = null) =>
        client.PostAsJsonAsync($"/sample/conversations/{conversation}/messages", new SendMessageRequest(Guid.NewGuid(), content, model));
}
