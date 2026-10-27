using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Filum.Evals.Mcp;

// A Claude Code config folder for a whole run, so the model sees nothing of the person running it: not their account's
// name or email, not Claude Code's own memory or CLAUDE.md files. The login is copied from the user's config, and
// renewed there, never in the copy. Claude Code fetches the account's profile into a new folder at its first start, so
// the folder is started once, its identity blanked before every session, and checked once before the first.
internal sealed partial class ClaudeIsolation(string? model, string home, Func<ProcessStartInfo, string, CancellationToken, Task<string>> run)
{
    private static readonly string[] Identity = ["emailAddress", "displayName", "fullName", "organizationName"];

    private readonly Lock _gate = new();
    private Task? _started;
    private string? _folder;
    private long? _loginExpiresAt;

    public ClaudeIsolation(string? model)
        : this(model, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), async (start, input, token) => (await ClaudeProcess.RunAsync(start, input, token)).Output)
    {
    }

    public string? Folder => _folder;

    /// <summary>Points a session at the isolated folder, ready to run.</summary>
    public async Task ApplyAsync(ProcessStartInfo start, CancellationToken cancellationToken)
    {
        Task started;
        lock (_gate)
        {
            started = _started ??= StartAsync(cancellationToken);
        }

        await started;
        await RenewAsync(cancellationToken);
        lock (_gate)
        {
            CopyLogin();
            Blank();
        }

        Point(start);
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        _folder = Directory.CreateTempSubdirectory(EvalsConstants.Isolation.FolderPrefix).FullName;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Delete();
        await RenewAsync(cancellationToken);
        lock (_gate)
        {
            CopyLogin();
        }

        await run(Session(), EvalsConstants.Isolation.WarmUp, cancellationToken);
        lock (_gate)
        {
            Blank();
        }

        // Claude Code may fetch the profile again on its next starts: blanked again and asked again, a few times.
        string? leaked = null;
        for (var attempt = 0; attempt < EvalsConstants.Isolation.Checks; attempt++)
        {
            if ((leaked = Leaked(await run(Session(), EvalsConstants.Isolation.Check, cancellationToken))) is null)
            {
                return;
            }

            lock (_gate)
            {
                Blank();
            }
        }

        Delete();
        throw new InvalidOperationException($"The agent still sees the account running it ({leaked}): its answers would not measure Filum alone.");
    }

    // What the model must not know, looked for in its answer to the check.
    private string? Leaked(string answer)
    {
        var account = ReadJson(Path.Combine(home, EvalsConstants.Isolation.ConfigFile))?[EvalsConstants.Isolation.Account];
        var field = Identity.FirstOrDefault(f => account?[f]?.GetValue<string>() is { Length: >= EvalsConstants.Isolation.MinIdentityLength } value
            && answer.Contains(value, StringComparison.OrdinalIgnoreCase));
        return field ?? (MemoryFolder().IsMatch(answer) ? "a memory folder" : null);
    }

    private ProcessStartInfo Session()
    {
        List<string> arguments = [EvalsConstants.AgentArguments.Print, EvalsConstants.AgentArguments.Tools, EvalsConstants.AgentArguments.NoTools];
        if (model is not null)
        {
            arguments.AddRange([EvalsConstants.AgentArguments.Model, model]);
        }

        var start = ClaudeProcess.Create(arguments, _folder!);
        Point(start);
        return start;
    }

    private void Point(ProcessStartInfo start)
    {
        start.Environment[EvalsConstants.Isolation.ConfigDirectory] = _folder;
        start.Environment[EvalsConstants.Isolation.DisableAutoMemory] = EvalsConstants.Isolation.On;
        start.Environment[EvalsConstants.Isolation.DisableClaudeMds] = EvalsConstants.Isolation.On;
    }

    private string CredentialsPath => Path.Combine(home, EvalsConstants.Isolation.UserFolder, EvalsConstants.Isolation.CredentialsFile);

    // A login that ends soon is renewed by a short session on the user's own config, never in the copy. With a token from
    // `claude setup-token` nothing is copied or renewed: a copy and the user's own Claude Code would renew one login in
    // turn, and each renewal ends the other's.
    private static bool HasToken => Environment.GetEnvironmentVariable(EvalsConstants.Isolation.OAuthToken) is { Length: > 0 };

    private async Task RenewAsync(CancellationToken cancellationToken)
    {
        if (!HasToken && ReadJson(CredentialsPath)?[EvalsConstants.Isolation.Login]?[EvalsConstants.Isolation.ExpiresAt]?.GetValue<long>() is { } end
            && DateTimeOffset.FromUnixTimeMilliseconds(end) < DateTimeOffset.UtcNow.AddMinutes(EvalsConstants.Isolation.RenewMinutes))
        {
            await run(ClaudeProcess.Create([EvalsConstants.AgentArguments.Print, EvalsConstants.AgentArguments.Tools, EvalsConstants.AgentArguments.NoTools], home), EvalsConstants.Isolation.WarmUp, cancellationToken);
        }
    }

    // The subscription's login only. Without a login file (the macOS keychain), a token or a key in the environment is
    // used as it is.
    private void CopyLogin()
    {
        if (HasToken)
        {
            return;
        }

        if (ReadJson(CredentialsPath)?[EvalsConstants.Isolation.Login] is not { } login)
        {
            if (Environment.GetEnvironmentVariable(EvalsConstants.Isolation.OAuthToken) is null && Environment.GetEnvironmentVariable(EvalsConstants.Isolation.ApiKey) is null)
            {
                throw new InvalidOperationException($"Claude Code's login was not found in {CredentialsPath}: run `claude setup-token` and set {EvalsConstants.Isolation.OAuthToken}.");
            }

            return;
        }

        var expiresAt = login[EvalsConstants.Isolation.ExpiresAt]?.GetValue<long>();
        var copy = Path.Combine(_folder!, EvalsConstants.Isolation.CredentialsFile);
        if (expiresAt != _loginExpiresAt || !File.Exists(copy))
        {
            _loginExpiresAt = expiresAt;
            File.WriteAllText(copy, new JsonObject { [EvalsConstants.Isolation.Login] = login.DeepClone() }.ToJsonString());
        }
    }

    private void Blank()
    {
        var path = Path.Combine(_folder!, EvalsConstants.Isolation.ConfigFile);
        var config = ReadJson(path) as JsonObject ?? [];
        var account = config[EvalsConstants.Isolation.Account] as JsonObject ?? [];
        foreach (var field in Identity)
        {
            account[field] = string.Empty;
        }

        config[EvalsConstants.Isolation.Account] = account;
        File.WriteAllText(path, config.ToJsonString());
    }

    private void Delete()
    {
        if (_folder is not null && Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static JsonNode? ReadJson(string path) => File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null;

    [GeneratedRegex(@"[\\/]\.claude[\\/]|[\\/]memory[\\/]")]
    private static partial Regex MemoryFolder();
}
