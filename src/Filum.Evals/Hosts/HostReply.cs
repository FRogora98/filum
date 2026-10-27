namespace Filum.Evals.Hosts;

// The error as "<status> <body>" when the request did not succeed.
internal sealed record HostReply<T>(int Status, T? Body, string? Error, double Seconds)
    where T : class;
