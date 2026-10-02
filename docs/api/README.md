# The endpoint groups' contract, v1

The groups of `Filum.Agent.Http` (conversations, usage, memory, skills, models) are a contract that apps rely on. A host maps them under the prefix it wants and with its own login, and its app (often a client generated from the description) calls them. This page says what a client may rely on.

- **The description:** [`openapi-v1.json`](openapi-v1.json), the OpenAPI document of the sample host (prefix `/sample`). A host's own document has its own prefix and adds its own routes; the groups' part is the same.
- **The version:** every response of the groups carries `Filum-Api-Version: 1`, errors and 401 included.

## What v1 guarantees

Within v1 a change may only **add**:
- an optional field in a payload, which a client that does not know it ignores;
- a new route;
- a new optional query parameter;
- a new error response, which a client treats like the errors it already handles.

Anything else needs **v2**:
- renaming or removing a field or a route;
- changing a field's type or meaning;
- making something required.

v2's groups are then mapped beside v1's, for as long as a host needs both, and the header says which version answered.

The snapshot is checked by `tests/Filum.Agent.Tests/OpenApiTests.cs`: a change to the contract fails the tests until `FILUM_UPDATE_SNAPSHOT=1 dotnet test` rewrites the file. The diff is then reviewed against this page.

## Conventions

- **Route names:** every route has a stable name, `filum.<group>.<action>`, which is its `operationId`.
- **Schemas** are named after the records (`MessageDto`, `StepDto`…).
- **Values:** fixed sets of values (a step's `kind`, a file's `sensitivity`) are strings. Moments are `date-time`, in UTC. Amounts are in USD.
- **Errors** are `application/problem+json`, with a human-readable `title`:
  - `400`: the request was refused, and the title says why;
  - `401`: no person;
  - `404`: not found, or another person's, which looks the same.
- **Numbers:** hosts should read numbers strictly (`JsonNumberHandling.Strict`, as the sample does), so a generated client gets exact types.

## What each group is for

| Group | Routes | A client uses it to |
|---|---|---|
| conversations | `filum.conversations.list / messages / send / delete` | list the person's chats, show one, send a message and show the answer with its steps (`data` on a host tool's step is that tool's card), delete a chat |
| usage | `filum.usage.month` | show what the person spent this month, by model |
| memory | `filum.memory.files / file / history / revision / write / sensitivity / delete / restore / undo / consolidate` | show what the assistant knows about the person (every file, its content, where it came from), its history, and let the person correct, hide, delete or undo; tidy the memory now (spec 030) |
| skills | `filum.skills.list / accept / decline` | list the person's procedures, and accept or decline one the assistant proposed |
| models | `filum.models.list` | let the person choose a model, when the host allows it |
