using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Filum.Agent.Http;

/// <summary>
/// Who is asking, decided by the host in one place: <paramref name="person"/> reads the person's id from a request (a
/// claim of the host's own login, for example). Without one the answer is 401, and nothing else in a request can name
/// a person.
/// </summary>
public static class PersonFilter
{
    private const string ItemKey = "Filum.Person";

    public static RouteGroupBuilder RequirePerson(this RouteGroupBuilder group, Func<HttpContext, Guid?> person)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            if (person(context.HttpContext) is not { } id || id == Guid.Empty)
            {
                return Results.Problem(title: "Sign in to continue.", statusCode: StatusCodes.Status401Unauthorized);
            }

            context.HttpContext.Items[ItemKey] = id;
            return await next(context);
        });

        return group;
    }

    /// <summary>The person of a request that passed <see cref="RequirePerson"/>.</summary>
    public static Guid PersonId(this HttpContext context) => (Guid)context.Items[ItemKey]!;
}
