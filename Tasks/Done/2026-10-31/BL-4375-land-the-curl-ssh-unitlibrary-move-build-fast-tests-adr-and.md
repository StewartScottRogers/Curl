---
id: BL-4375
title: Land the Curl.Ssh.UnitLibrary move: build, fast tests, ADR and README row
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320]
touches: [Curl.Ssh.UnitLibrary, Curl.Ssh.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests/SshWireDecodersTests.cs, Curl.Protocol.Ssh.UnitTests/SshWireDecodersAdversarialTests.cs, Curl.Protocol.Ssh.UnitTests/Fakes/RepeatingRandomSource.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings, Curl.Protocol.Ssh.UnitTests, Curl.Protocol.Ssh.UnitTests/Curl.Protocol.Ssh.UnitTests.csproj, Curl.slnx, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests, Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.PlatformBuilds.cs, Curl.Engine.UnitTests/EngineCompositionPlatformLibraryTests.cs, Curl.Protocol.Ssh.IntegrationTests/Curl.Protocol.Ssh.IntegrationTests.csproj, Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4375 — Land the Curl.Ssh.UnitLibrary move: build, fast tests, ADR and README row

## Goal

The stashed work of BL-4362 (the shared SSH layer in `Curl.Ssh.UnitLibrary`, with `Curl.Protocol.Ssh.UnitLibrary` referencing it) builds cleanly, passes the fast tests and is recorded in an ADR.

## Context

Split from BL-4362 (over its cost cap). The stash holds all the work, already verified once on its work tree: root types and the transport layer moved to `Curl.Ssh.UnitLibrary`, `SshWireDecoders` kept in `Curl.Protocol.Ssh` (ADR-1162, the audit fuzzer's entry point, ADR-0394), `Curl.Ssh.UnitLibrary/CLAUDE.md`, the ADR-0120 table row and `ProtocolIsolationTests.cs`. This task builds, runs the fast tests, fixes what breaks and finishes. Do not redo the move.

## Acceptance criteria

- [x] `Curl.Ssh.UnitLibrary` and `Curl.Ssh.UnitTests` exist in `/Wire/Ssh/` in `Curl.slnx` and the library references no `Curl.Protocol.*` library other than `Curl.Protocol.Abstractions.UnitLibrary`.
- [x] ISshRandomSource, SshConnectionLostException, Libssh2ErrorCode and SshTransferException (and their tests) live in `Curl.Ssh.UnitLibrary` / `Curl.Ssh.UnitTests`; `Curl.Protocol.Ssh.UnitLibrary` references the new library and no longer holds them; `SshWireDecoders` stays in `Curl.Protocol.Ssh` as ADR-1162 records.
- [x] An ADR records the shared SSH layer and the planned splits, and is listed in the ADR README.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4362.
- Continues the stashed work of BL-4362.
- The quality gates (BL-4362's fourth criterion) are BL-4376.
- 2026-10-30 run (lane 1): the stash no longer applied over BL-4320's work (BL-4366 split `SshDiagnosticLog` into partial files, BL-4367 moved the test usings into GlobalUsings). Applied it as a three-way patch: using-only conflicts kept HEAD in `Curl.Protocol.Ssh.UnitTests` (which now imports `Curl.Ssh`, `Curl.Ssh.Fakes`, `Curl.Ssh.HostKeys` and the moved namespaces from GlobalUsings, the old per-namespace files renamed) and the stash in `Curl.Ssh.UnitTests`; `SshDiagnosticLog` keeps its partials, wraps `SshTransportLog`, and loses `SshDiagnosticLog.Transport.cs` and `HostKeyPresented`, now the transport log's.
- Touches widened to the whole `Curl.Protocol.Ssh.UnitTests`: the moved namespaces reach its test files' static usings and GlobalUsings; no task in Doing on work/dark-factory names it.
- ADR renumbered 1162 to 1165: ADR-1162 is BL-4367's global-using ADR.
- Verified: `dotnet build Curl.slnx` 0 warnings 0 errors; fast tests 112 assemblies passed, 0 failed (Curl.Ssh.UnitTests 431, Curl.Protocol.Ssh.UnitTests 1511); `Measure-CodeQuality.ps1 -Library Curl.Ssh.UnitLibrary,Curl.Protocol.Ssh.UnitLibrary` 0 failing members, so BL-4376 may find nothing left to do.
- Criterion on CI on Linux and macOS: the change moves files and namespaces only, platform-neutral.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: e09ab28e 38fdd9b6 10174c6c 3d836d5e.
- 2026-10-30: Doing -> Done. Curl.Ssh.UnitLibrary holds the SSH transport layer; Curl.Protocol.Ssh builds on it, build clean and fast tests green
