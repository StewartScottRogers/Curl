---
id: BL-1598
title: Record the IntegrationTests project rule in an ADR, CLAUDE.md, the glossary and the project skills
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md, Documentation/Planning/Decisions, Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md, MSTestSettings.cs, .claude/skills/new-project, .claude/skills/verify, .claude/agents/coverage-auditor.md]
requirement: none
created: 2026-10-07
completed:
---
# BL-1598 — Record the IntegrationTests project rule in an ADR, CLAUDE.md, the glossary and the project skills

## Goal

Every document an agent reads before writing a test states the IntegrationTests rule Stewart approved on 2026-10-07, one ADR records it with the decisions that come with it, and nothing in those documents still says Integration tests live in `*.UnitTests` projects.

## Context

**The rule (Stewart, approved 2026-10-07).**

- An integration test touches something real outside the process: a socket, the disk, the OS, a native API, a system agent. It carries `[TestCategory("Integration")]`.
- Integration tests live only in projects named `Curl.<Area>.IntegrationTests`. A `*.UnitTests` project contains no `[TestCategory("Integration")]` test. Every test in an `*.IntegrationTests` project carries the Integration category, so `dotnet test --filter "TestCategory!=Integration"` still skips all of them.

**Decisions that come with it, to record in the ADR** (decided by Claude under Stewart's delegation; the first three were made while planning this work on 2026-10-07, the fourth is this task's to make):

1. **Sort order.** `Curl.<Area>.IntegrationTests` sorts alphabetically before `Curl.<Area>.UnitLibrary`, so "library, then its IntegrationTests, then its UnitTests" is impossible in the flat alphabetical `Curl.slnx`. Names stay as they are; the naming rule says the IntegrationTests project sits in the flat alphabetical run immediately before its library (for `Curl.Console`, immediately before `Curl.Console.UnitTests`, which already sorts before `Curl.Console`). Example in the tree today: lines 70-72 of `Curl.slnx`, `Curl.Networking.IntegrationTests`, `Curl.Networking.UnitLibrary`, `Curl.Networking.UnitTests`.
2. **CI needs no change.** Measured 2026-10-07 (.NET SDK 10.0.401, MSTest 4.4.1, `dotnet test` in VSTest mode): `dotnet test Curl.Protocol.Dict.UnitTests --no-build --filter "TestCategory=NoSuchCategoryXyz"` prints "No test matches the given testcase filter" and exits 0. So `.github/workflows/ci.yml`'s `dotnet test -c Release --no-build --filter "TestCategory!=Integration"` passes over an `*.IntegrationTests` project whose tests are all filtered out, and `.github/workflows/integration.yml` runs `dotnet test -c Release --no-build --filter "TestCategory=Integration"` against the whole solution, so it picks up every new `*.IntegrationTests` project in `Curl.slnx` without an edit. `ci.yml` is a guard file; this decision means no task needs to touch it. (CI run 37650397456 failed on `6ecb2781f` for a different reason, a duplicate `Parallelize` attribute, fixed by BL-1597.)
3. **Long-running tests that touch nothing outside the process are not Integration tests.** `Curl.Cryptography.UnitTests` has four tests tagged `Integration` only because they are slow: `X25519Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK`, `X448Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK`, `Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB` and `BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP384r1AndP512r1Vector_GivesItsExpectedResult`. They are pure computation, so under the rule they stay in `Curl.Cryptography.UnitTests` and lose the Integration tag. They cannot simply join the fast run: a million X25519 operations at even the best constant-time C# rate (tens of microseconds each) is tens of seconds, and BL-1525's partial speed-up measured 5 min 40 s (X25519), 14 min 51 s (X448) and 32.9 s (CAST-128 B.2) on 2026-10-07, against `TestDiagnostics`' 3-second `SLOW:` budget (ADR-0417). Creating `Curl.Cryptography.IntegrationTests` for them would contradict the rule's definition. Decision: such a test carries `[TestCategory("LongRunning")]` and a custom MSTest condition attribute (derived from `ConditionBaseAttribute`, as `OSCondition` is) that skips it unless the environment variable `CURL_RUN_LONG_RUNNING_TESTS` is `1`. The fast command stays `dotnet test --filter "TestCategory!=Integration"` (they show as skipped there), and `integration.yml` (not a guard file) runs them with the variable set. Why this over a new excluded category: changing the fast filter would change `ci.yml`, `RunDarkFactory.ps1`, `Measure-CodeQuality.ps1`, the `verify` skill and hundreds of task criteria that quote the command. Any long-running test that drops under 3 s in a Debug build simply loses both attributes and joins the fast run. BL-1604 applies this to the four tests and depends on this task.
4. **Untagged tests that touch the disk or a socket: decide.** The rule's definition names the disk and sockets, but the solution has many untagged fast tests that do both on purpose, so the thin adapters reach 100% coverage on the fast run: `Curl.Core.UnitTests/FileSystem/PhysicalFileSystemTests.cs`, `Curl.Console.UnitTests/PhysicalOutputPathsTests.cs`, `DiskWriteOutFileOpenerTests.cs`, `KerberosDiskFileReaderTests.cs` and `KerberosDiskFileWriterTests.cs` (each says so in its class doc comment), and ADR-0083's "fast tests that open local sockets without sending anything" in `Curl.Networking.UnitTests`. Decide, by the standing rules, whether the rule covers them (temporary-directory files; local sockets opened but sent nothing), and write the decision and its reason into the ADR. If they are Integration tests, state how the 100% fast-run coverage gate is met for the adapters they cover (a seam, or ADR-0083-style `[ExcludeFromCodeCoverage]` with a justification), and have `task-planner` file one move task per affected `.UnitTests` project, listed in Notes. Do not move any test in this task.

**Places that must state the rule** (read each before editing; `align-and-document` owns the wording):

- Root `CLAUDE.md`: "Repository layout" (the tree gains an IntegrationTests example and the "66 projects" count becomes the true count), "Project naming" (the `Curl.<Area>.IntegrationTests` name and its sort position), "Build and test commands" (the fast command skips every `*.IntegrationTests` test; `dotnet test --filter "TestCategory=Integration"` runs them; `CURL_RUN_LONG_RUNNING_TESTS=1` with `--filter "TestCategory=LongRunning"` runs the long-running ones), and "Quality gates" (coverage is measured from the fast tests only, so moving a test into an IntegrationTests project never changes a library's measured coverage, and a line only an Integration test reaches is uncovered).
- `Documentation/Planning/Decisions/`: a new ADR with the next free number, titled for the rule, marked "Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions")" for decisions 1-4 and naming Stewart's approval of the rule itself (2026-10-07). It names ADR-0083 and ADR-0118 (whose "Tests" section says multi-million-iteration vectors are tagged Integration) and says which of their statements it supersedes; add a one-line "Superseded in part by ADR-####" note to each of those two ADRs' status lines.
- `Documentation/Wiki/Glossary.md`: entries for "Integration test" (the definition above) and "IntegrationTests project", and "LongRunning test" for decision 3, each pointing at the ADR.
- `Documentation/Product/Product-Overview.md`: the "Unit tests need no network" principle (around line 243) and the diagram note "No [TestCategory("Integration")]" (around line 156) say where Integration tests live.
- Root `MSTestSettings.cs`: its header comment says it is shared by every `.UnitTests` project, that the tests need no network, and counts 24 test projects; make it true for both `*.UnitTests` and `*.IntegrationTests` projects (`Directory.Build.props` links it into both), with no count that goes stale. Comment only, no code change.
- `.claude/skills/new-project/SKILL.md`: how to add a `Curl.<Area>.IntegrationTests` project when an area first needs one (copy `Curl.Networking.IntegrationTests.csproj`: the MSTest `PackageReference`, the `Using`, one `ProjectReference` to the library; no `MSTestSettings.cs` of its own; an `InternalsVisibleTo` in the library's csproj only when the tests use internals; add it to `Curl.slnx` in its alphabetical place).
- `.claude/skills/verify/SKILL.md`: step 5 runs the Integration tests "if the change touched integration-level code"; make it name the area's `*.IntegrationTests` project.
- `.claude/agents/coverage-auditor.md`: read it; if it assumes every test project is `*.UnitTests` or that Integration tests sit beside fast ones, make it say coverage comes from the fast run and that IntegrationTests projects contribute none. If it needs no change, say so in Notes.

Out of scope, each its own task: `Measure-CodeQuality.ps1` (BL-1605), `RunDarkFactory.ps1` (BL-1606), `Audit/Tools/Find-WeakTests.ps1` (BL-1607, interactive only), the moves (BL-1599 Cli, BL-1600 Console, BL-1601 Core, BL-1602 SSH; Networking was moved by Stewart in `6ecb2781f` and made buildable by BL-1597), the build-time check (BL-1603, which adds its own sentence to `CLAUDE.md`) and the Cryptography retag (BL-1604). Name them in the ADR's Consequences.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` states the rule, decisions 1-4 above each with its reason, the CI measurement in decision 2 with the SDK and MSTest versions, and the "Decided by Claude under Stewart's delegation" line; ADR-0083 and ADR-0118 each carry a "Superseded in part by" line naming it.
- [ ] Root `CLAUDE.md` "Repository layout", "Project naming", "Build and test commands" and "Quality gates" each state the part of the rule listed in Context, and the project count in "Repository layout" matches the number of `<Project` entries in `Curl.slnx`.
- [ ] `Documentation/Wiki/Glossary.md` has entries "Integration test", "IntegrationTests project" and "LongRunning test".
- [ ] `Documentation/Product/Product-Overview.md`, root `MSTestSettings.cs` (comment only), `.claude/skills/new-project/SKILL.md` and `.claude/skills/verify/SKILL.md` state where Integration tests live; `grep -rn "Integration" CLAUDE.md Documentation/Product .claude/skills` finds no statement that Integration tests sit in a `*.UnitTests` project.
- [ ] Notes record whether `.claude/agents/coverage-auditor.md` changed and why, and, if decision 4 makes more tests Integration tests, the IDs of the move tasks filed for them.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green after the `MSTestSettings.cs` comment change.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
