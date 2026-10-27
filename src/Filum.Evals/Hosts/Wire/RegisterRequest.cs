namespace Filum.Evals.Hosts.Wire;

// The common auth contract the hosts that register accounts share.
internal sealed record RegisterRequest(string Email, string Password);
