using System.Net;
using System.Net.Http.Json;

namespace Filum.Evals.UnitTests.Fakes;

// Each request goes to the first route whose method and path start match.
internal sealed class StubHost : HttpMessageHandler
{
    private readonly List<(HttpMethod Method, string Path, Func<HttpRequestMessage, HttpResponseMessage> Answer)> _routes = [];

    public List<(HttpMethod Method, string Path, string? Authorization, string? Body)> Requests { get; } = [];

    public StubHost On(HttpMethod method, string path, Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        _routes.Add((method, path, answer));
        return this;
    }

    public StubHost On(HttpMethod method, string path, object body) => On(method, path, _ => Json(body));

    public StubHost On(HttpMethod method, string path, HttpStatusCode status, string body = "") =>
        On(method, path, _ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    public HttpClient Client() => new(this) { BaseAddress = new Uri("http://host.test/") };

    public static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, path, request.Headers.Authorization?.ToString(), body));
        var route = _routes.FirstOrDefault(r => r.Method == request.Method && path.StartsWith(r.Path, StringComparison.Ordinal));
        return route.Answer is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : route.Answer(request);
    }
}
