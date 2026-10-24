---
id: BL-3389
title: Remove ModelContextProtocol and Microsoft.Extensions.Hosting from the solution and bring the documents true
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3388]
touches: [Curl.Mcp.Protocol.UnitLibrary/README.md, Curl.Mcp/Curl.Mcp.csproj, Directory.Packages.props, Directory.Build.props, CLAUDE.md, DarkFactory/LaneRules.md, README.md, Curl.Mcp/CLAUDE.md, Curl.Mcp/README.md, Curl.Mcp.UnitLibrary/CLAUDE.md, Documentation/Planning/Curl-Mcp.md]
model: sonnet
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3389 — Remove ModelContextProtocol and Microsoft.Extensions.Hosting from the solution and bring the documents true

## Goal

Neither `ModelContextProtocol` nor `Microsoft.Extensions.Hosting` is referenced anywhere in the solution, and every document that named them as an approved exception says the solution takes no package but MSTest.

## Context

- Stewart, 2026-10-23. Follows BL-3388 (the cut-over) and the decision of BL-3373.
- Remove the two `PackageReference` lines and the comment above them from `Curl.Mcp/Curl.Mcp.csproj`, and the two `PackageVersion` lines from `Directory.Packages.props` (check no other project, `obj/project.assets.json`-visible transitive pin or `Directory.Packages.props` `PackageVersion` for a transitive-only dependency of those two remains unused; remove such pins too). If `Directory.Build.props` has wording or a target that allows these packages in `Curl.Mcp` only, drop it (a grep currently finds none).
- Rewrite the wording in root `CLAUDE.md` (BCL rule: the approved package list becomes MSTest only), `DarkFactory/LaneRules.md` (lines 80-81, the same rule, because lane runs load it in place of CLAUDE.md), root `README.md`, `Curl.Mcp/CLAUDE.md`, `Curl.Mcp/README.md`, `Curl.Mcp.UnitLibrary/CLAUDE.md` and `Documentation/Planning/Curl-Mcp.md` so each is true of the code as it is: the hand-written server in `Curl.Mcp.Protocol.UnitLibrary`, the tool adapters in `Curl.Mcp/Tools/`, the host `McpStdioHost`. Leave ADRs alone except that ADR-0548's supersession note (written by BL-3373) may be checked for accuracy; ADRs are history.
- Memory files outside the repository are not edited here; mention in Notes that the memory note `bcl-only-no-third-party-packages` still lists the Curl.Mcp exception for Stewart's session to update.
- Prove behaviour is unchanged: run the golden conversations, the full Curl.Mcp integration tests and a native AOT publish. Per the BL-2486 notes, AOT publish needs `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH for vswhere.

## Acceptance criteria

- [x] `grep -rIn "ModelContextProtocol\|Microsoft.Extensions.Hosting" . --include=*.csproj --include=*.props --include=*.cs --include=*.slnx --include=*.json --include=*.yml --exclude-dir=bin --exclude-dir=obj --exclude-dir=Tasks --exclude-dir=.git` finds nothing; `*.md` hits remain only in ADRs and the ADR README as history.
- [x] `dotnet restore Curl.slnx` and `dotnet build Curl.slnx -warnaserror` are clean with the two packages gone, and `dotnet list Curl.Mcp package` shows none.
- [x] `dotnet publish Curl.Mcp -r win-x64 -p:PublishAot=true` builds with no IL or trim warning, and the published binary passes `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration"` including the golden conversations and `CurlMcpNativeAotIntegrationTests`; commands and results are in Notes.
- [x] `CLAUDE.md` and `DarkFactory/LaneRules.md` say MSTest is the only approved package and name no `Curl.Mcp` exception; the two files agree.
- [x] `Curl.Mcp/CLAUDE.md`, `Curl.Mcp/README.md`, `Curl.Mcp.UnitLibrary/CLAUDE.md` and `Documentation/Planning/Curl-Mcp.md` describe the hand-written server and mention neither package except as history.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [x] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes
- Added `Curl.Mcp.Protocol.UnitLibrary/README.md` to touches: it said the packages were still to be replaced; no other Doing task names it.
- Grep of the criterion finds nothing in csproj/props/cs/slnx/json/yml. `dotnet build Curl.slnx -warnaserror`: 0 warnings, 0 errors. `dotnet list Curl.Mcp package`: only the auto-referenced SDK packages (ILCompiler, ILLink.Tasks).
- `dotnet publish Curl.Mcp -r win-x64 -p:PublishAot=true` (vswhere dir on PATH): no warnings. `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration"`: 42 passed, 0 failed (golden conversations and CurlMcpNativeAotIntegrationTests included).
- `Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary`: 0 failing members. Fast tests: Curl.Mcp.UnitTests 526 passed, Curl.Mcp.Protocol.UnitTests 157 passed.
- Memory note `bcl-only-no-third-party-packages` outside the repository still lists the Curl.Mcp exception; Stewart's session should update it.
- ADR-0548's supersession note was not changed (ADRs are history).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f6c02ba2 f382b46d 97b19bc2 8d48f09f 7b643936 96ffe31b 6d2cec38.
- 2026-10-23: Doing -> Done. Curl.Mcp takes no package; documents say MSTest is the only one
