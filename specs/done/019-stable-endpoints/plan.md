# Stable endpoints for apps — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (acceptance criteria agreed, open questions emptied).

## Approach

1. **Metadata on every route** of `FilumEndpoints`, through one private helper:
   - `WithName("filum.<group>.<action>")`, which ASP.NET Core's OpenAPI uses as the `operationId`;
   - `WithSummary`, `WithTags(<group>)`;
   - `Produces<T>(status)` for each success, and `ProducesProblem(status)` for each error the handler can return.
2. **`FilumApi`** holds `Version = "1"` and `Header = "Filum-Api-Version"`.
   - An endpoint filter on each route sets the header.
   - `RequirePerson` sets it too, so a 401 carries it.
3. **The sample host** adds `Microsoft.AspNetCore.OpenApi` and serves `/sample/openapi/v1.json`. The engine's libraries take no new package.
4. **Snapshot:** `OpenApiTests` in `Filum.Agent.Tests` fetches the document and compares it, as normalized JSON, with `docs/api/openapi-v1.json`. `FILUM_UPDATE_SNAPSHOT=1` rewrites the file.
5. **`docs/api/README.md`** gives the guarantees of v1, what needs v2, the header, and what each group is for.

## Contract changes

- **Response header:** `Filum-Api-Version: 1`.
- **Route names:** `filum.*`.
- **New documents:** `docs/api/openapi-v1.json` and `docs/api/README.md`.
- No route or payload changes.

## Test strategy

| Criterion | Test |
|---|---|
| 1 | `OpenApiTests`: every route has an `operationId` (unique), a summary and a tag, and its responses |
| 2 | `OpenApiTests`: document equals the snapshot |
| 3 | header on a success, a 404 and a 401 |
| 4 | private tests unchanged |
| 5 | the README, read |
| 6 | catalog snapshot test |
