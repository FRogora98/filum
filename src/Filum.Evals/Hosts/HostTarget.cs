namespace Filum.Evals.Hosts;

// A new synthetic person is made by registering an account at the host's own path or by a header with a new id; the
// host's own command runs after the person exists ({email} and {person} replaced). Every host names its own: there is no
// default service or registration path.
internal sealed record HostTarget(string Prefix = HostTarget.DefaultPrefix, string? Register = null, string? PersonHeader = null, string? Setup = null)
{
    // No model is named in the messages.
    public const string HostModel = "host";

    public const string DefaultPrefix = "/api";

    public static HostTarget Stock { get; } = new();

    public string Describe(Uri? service) =>
        $"{service}{Prefix.TrimStart('/')} · {(PersonHeader is null ? $"register {Register}" : $"header {PersonHeader}")}{(Setup is null ? "" : " · setup")}";

    public string Url(string path) => $"{Prefix.TrimEnd('/')}{path}";
}
