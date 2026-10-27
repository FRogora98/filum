# Exceptions to the coding standard

A MUST rule of the coding standard that the code does not follow is listed here, with the file and the reason, and is
approved by the technical lead. Each entry names its rule.

| Rule | File | Reason | Status |
|---|---|---|---|
| SVC-04 | `src/Filum.Engine/Tools/ToolCatalog.cs` | The order of the tools is part of what the model reads, so it is written once and in full in one list rather than derived from registration order. | Approved by the maintainer, 2026-10-07 |
| SVC-08 | `src/Filum.Engine/Memory/IMemoryService.cs` (and its partial `IMemoryService.Roles.cs`) | The interface shipped in a pre-release in the namespace `Filum.Engine.Memory`, and compatibility (SDK-04) comes before the folder rule. It stays there although `Memory/` holds more than 8 files; its role interfaces live in `Memory/Contracts/`. It moves to `Memory/Contracts/` in 2.0.0, the next major version. | Approved by the maintainer, 2026-10-08 |
| SVC-08 | `src/Filum.Engine/Tools/IMemoryToolsFactory.cs` | Shipped in a pre-release in the namespace `Filum.Engine.Tools`, like `IMemoryService`: it stays although `Tools/` holds more than 8 files, and moves to `Tools/Contracts/` in 2.0.0, the next major version. | Approved by the maintainer, 2026-10-08 |
| ARC-01, ARC-03 | `src/Filum.Engine/Collections/Compute/Jint/`, `src/Filum.Engine/Packs/FileSystem/` | The interpreter of `compute_run` (Jint) and the reading of a package's folder stay in `Filum.Engine` until 2.0.0, the next major version, so that no host has to add a package to keep what 1.0 gave it. Each sits behind an internal interface (`ICodeSandbox`, `IPackSource`) in a folder of its own, and the rest of the engine depends only on the interface. | Approved by the maintainer, 2026-10-08 |
| CFG-01 | `src/Filum.Mcp/Configuration/McpOptions.cs` | filum-mcp is configured by `FILUM_*` environment variables, which every MCP host can set, so its options are bound at the configuration's root, with no section. The rest of the rule holds: the variables are bound to the sealed `McpOptions`, validated at start, and a wrong value stops the server. | Approved by the maintainer, 2026-10-08 |
| CFG-02 | `src/Filum.Engine.Storage.LocalFolder/LocalFolderStore.cs` (`DefaultFolder()`) | Shipped in a pre-release, it reads `FILUM_HOME` from the environment. It is kept, marked obsolete, until 2.0.0, the next major version, when it is removed: the host decides the folder. No other code of the libraries reads an environment variable. | Approved by the maintainer, 2026-10-08 |

## Notes

- **SVC-04, the tool order.** Adding a tool means adding its method to a tool group, its constant to `ToolNames`, and
  its place in `ToolCatalog.Order`. The unit test `ToolCatalogTests.Order_WhenComparedWithTheToolsOfEveryGroup_ShouldNameEachExactlyOnce`
  fails when a tool is missing from the list or appears in it twice, and the tool catalog snapshot shows the change.
- **ARC-01, ARC-03, the interpreter and the package folder.** The architecture tests
  `ArchitectureTests.Engine_WhenBuilt_ShouldReachTheInterpreterOnlyThroughItsSandbox` and
  `ArchitectureTests.Libraries_WhenBuilt_ShouldTouchFilesOnlyWhereThatIsTheirJob` fail when Jint is used outside
  `Collections/Compute/Jint`, or a file or folder API outside `Packs/FileSystem`, anywhere in the engine.
- **CFG-01, filum-mcp's settings.** The integration test `Start_WhenASettingIsNotAFolderPath_ShouldStopTheServerBeforeAnyProtocolOutput`
  shows a wrong value stopping the server with exit code 1 and nothing on stdout.
