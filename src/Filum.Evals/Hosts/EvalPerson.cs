using System.Net.Http.Headers;

namespace Filum.Evals.Hosts;

internal sealed record EvalPerson(Guid Id, string Email, string? Token, string? Header)
{
    // A synthetic address no mail reaches.
    public static EvalPerson New(string? header = null)
    {
        var id = Guid.NewGuid();
        return new EvalPerson(id, $"eval-{id:N}@example.invalid", null, header);
    }

    public void Apply(HttpRequestMessage request)
    {
        if (Token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(EvalsConstants.Http.BearerScheme, Token);
        }

        if (Header is not null)
        {
            request.Headers.Add(Header, Id.ToString());
        }
    }
}
