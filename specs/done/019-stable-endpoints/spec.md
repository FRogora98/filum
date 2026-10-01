# Stable endpoints for apps

> Status: Implemented
> Area: hosting / HTTP
> Author/date: federico rogora (drafted by Claude), 2026-10-01
> Roadmap: 019, synchronized with the first product that hosts the engine (its app's "what the coach knows about you" and usage screens are the first outside client)

## Problem

Since spec 017 any host maps the engine's endpoint groups (conversations, usage, memory, skills, models), and two apps will call them: the hosted product's own app, and the first host's app. Today that contract exists only as code:
- **Nothing says what a client may rely on.** No version, no description, no list of routes, fields and errors.
- **Nothing stops a change from breaking a client.** A renamed field or a moved route would compile, pass the engine's tests, and break an app in production, in a repository that is not this one.
- **An app's developer reads the source** to learn a payload, and a mobile client cannot be generated.

## Goal

The groups are a versioned, documented contract (v1):
- an OpenAPI description any host can serve;
- a committed snapshot, so a change to the contract is seen and reviewed like the tool catalog;
- a rule for what a change may do without a new version.

## Non-goals

- **No change to the routes or payloads of today:** v1 *is* today's contract, described.
- **No generated client:** a client can be generated from the description, but none is kept here.
- **No pagination, filters or new endpoints:** they come when an app needs them, as additive changes.
- **No authentication in the description beyond "the host's own":** each host documents its own login.
- **No public hosted API:** the contract is for hosts and their own apps.

## Current behavior

- `FilumEndpoints.MapFilumConversations | Usage | Memory | Skills | Models` (`src/Filum.Agent.Http/FilumEndpoints.cs`) map minimal-API routes with no names, summaries or declared responses. The payload records are in `Dtos.cs` and `ConversationDtos.cs`, and errors are problem details with a `title`.
- The hosted product maps them under `/api`; the first host will map them under `/api/v1`.
- Nothing describes them, and no test fails when a route or field changes.

## Desired behavior

**The contract, v1.**
- Each route has a stable name (for example `filum.conversations.send`), a one-line summary, a tag per group, and every response it can give (status and payload type).
- A problem response is documented as `application/problem+json` with its `title`.
- **The description is fit for generated clients**, since the first host generates its app's client from it:
  - the route's name is its `operationId`, unique and stable;
  - every payload schema has a stable name (the record's name);
  - fixed sets of values are strings;
  - a value that is only a day is `format: date`, a moment `format: date-time`.
- `FilumApi.Version` is `"1"`. Every response of the groups carries the header `Filum-Api-Version: 1`, so a client can check what it talks to.

**The description.**
- A host that wants it serves an OpenAPI document of the groups with the standard ASP.NET Core support. The sample host serves it at `/sample/openapi/v1.json`.
- The document holds only the engine's groups and what the host adds; the engine adds no route to serve it.

**The snapshot.**
- `docs/api/openapi-v1.json` in this repository is the sample host's document.
- A test regenerates it and compares: a difference fails, like the tool catalog snapshot. A deliberate change is written with `FILUM_UPDATE_SNAPSHOT=1` and reviewed in the diff.

**The rule for changes**, written in `docs/api/README.md`:
- Within v1, a change may only add: an optional field, a new route, a new optional query parameter, a new response a client can treat as an error.
- Renaming or removing a field or route, changing a type, or making something required needs v2. Then v2's groups are mapped beside v1's for as long as a host needs both, and the header says which.
- `docs/api/README.md` also lists, per group, what a client typically does with it:
  - show a person's memory and its history;
  - undo a change;
  - accept a skill proposal;
  - show usage this month.

**The hosts.**
- The hosted product's app keeps working unchanged: same routes and payloads, plus the header.
- The first host maps the groups under its own prefix, and its app can rely on the snapshot of the version it maps.

## Platform check

1. **Generic:** yes. The contract describes generic groups (memory, skills, conversations, usage, models) and names no domain; a host's own routes are its own.
2. **Sensitivity and privacy:**
   - the description holds no data, only shapes;
   - every route still serves only the person the host names;
   - private files keep their sensitivity in the payloads, as today.

## Acceptance criteria

1. Given the sample host, when its OpenAPI document is generated, then it lists every route of the five groups with a unique `operationId`, a summary, a tag, and each response's status and payload type, every payload schema named after its record.
2. Given `docs/api/openapi-v1.json`, when the tests run, then the generated document equals it; given a renamed field in a payload, then the test fails until the snapshot is regenerated.
3. Given any response of a mapped group (success or problem), when a client reads its headers, then `Filum-Api-Version` is `1`.
4. Given the hosted product, when its tests run, then every route and payload is unchanged; the only difference is the new header.
5. Given `docs/api/README.md`, when a developer reads it, then it states what v1 guarantees, what needs v2, and what each group is for.
6. Given the catalog snapshot, when the contract is added, then the snapshot is unchanged.

## Open questions

None.
