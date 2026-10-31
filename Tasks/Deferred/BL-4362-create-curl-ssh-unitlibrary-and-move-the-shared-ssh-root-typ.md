---
id: BL-4362
title: Create Curl.Ssh.UnitLibrary and move the shared SSH root types into it
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320]
touches: [Curl.Ssh.UnitLibrary, Curl.Ssh.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests/SshWireDecodersTests.cs, Curl.Protocol.Ssh.UnitTests/SshWireDecodersAdversarialTests.cs, Curl.Protocol.Ssh.UnitTests/Fakes/RepeatingRandomSource.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Ssh.cs, Curl.Protocol.Ssh.UnitTests/Curl.Protocol.Ssh.UnitTests.csproj, Curl.slnx, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests, Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.PlatformBuilds.cs, Curl.Engine.UnitTests/EngineCompositionPlatformLibraryTests.cs, Curl.Protocol.Ssh.IntegrationTests/Curl.Protocol.Ssh.IntegrationTests.csproj, Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs, Documentation/Planning/Decisions/ADR-0120-protocol-libraries-may-reference-the-hand-built-libraries-and-nothing-else-horizontal.md]
requirement: none
created: 2026-10-30
completed:
---
# BL-4362 — Create Curl.Ssh.UnitLibrary and move the shared SSH root types into it

## Goal

A new shared Curl.Ssh.UnitLibrary and Curl.Ssh.UnitTests exist in /Wire/Ssh/ in Curl.slnx and hold the shared SSH root types, with Curl.Protocol.Ssh.UnitLibrary referencing it and behaving as before.

## Context

Part 1 of BL-4328 (SSH transport layer into a shared library so extensions such as BL-4145 may use it, ADR-0999). Use the new-project skill. Move ISshRandomSource, SshWireDecoders, SshConnectionLostException, Libssh2ErrorCode and SshTransferException with their tests from the protocol library root into namespace Curl.Ssh. Add InternalsVisibleTo. Record the layer in an ADR (Decided by Claude under Stewart's delegation), including the planned SshDiagnosticLog split and the HostKeys verifier move done by the sibling tasks.

## Acceptance criteria

- [x] `Curl.Ssh.UnitLibrary` and `Curl.Ssh.UnitTests` exist in `/Wire/Ssh/` in `Curl.slnx` and the library references no `Curl.Protocol.*` library other than `Curl.Protocol.Abstractions.UnitLibrary`.
- [x] ISshRandomSource, SshConnectionLostException, Libssh2ErrorCode and SshTransferException (and their tests) live in `Curl.Ssh.UnitLibrary` / `Curl.Ssh.UnitTests`; `Curl.Protocol.Ssh.UnitLibrary` references the new library and no longer holds them; `SshWireDecoders` stays in `Curl.Protocol.Ssh` as ADR-1162 records.
- [x] An ADR records the shared SSH layer and the planned splits, and is listed in the ADR README.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-30 (BL-4367): `touches` narrowed from the whole Curl.Protocol.Ssh.UnitTests to the files and folders this task changes there. That project now imports each moving namespace once, in `GlobalUsings/Curl.Protocol.Ssh.<Folder>.cs`, so moving a folder changes that one file (to `global using Curl.Ssh.<Folder>;`) rather than the using lines of every test that names its types; root types moved to Curl.Ssh get their own new `GlobalUsings/Curl.Ssh.cs`.

- 2026-10-30: Split from BL-4328 (over its cost cap); see its Notes for the dependency closure measured. Each moved folder's namespaces become `Curl.Ssh.<Folder>`; every moved type is `internal`, so `Curl.Ssh.UnitLibrary` needs `InternalsVisibleTo` for the consumers listed in BL-4328's Notes.
- Continues the stashed work of BL-4328.
- 2026-10-30 (lane 2): The stash applied to this claim already holds all of BL-4328, not only part 1: the whole transport layer (root types, Transport, KeyExchange, PacketProtection, Compression, Negotiation, the HostKeys signature verifiers) is in `Curl.Ssh.UnitLibrary`, and `SshDiagnosticLog` is split over `Curl.Ssh.Transport.SshTransportLog` (BL-4363's work). Kept it rather than undo three quarters of working code; BL-4363, BL-4364 and BL-4365 are deferred until this lands, then their next look finds nothing left.
- Criterion 2 reworded (so this task went back to Backlog for it): `SshWireDecoders` cannot move. It is the public entry point the audit fuzzer calls as `Curl.Protocol.Ssh.SshWireDecoders` (ADR-0394), which a lane cannot edit, and it decodes SFTP attributes, a protocol-library type. ADR-1162 records it.
- Touches widened: `Curl.Protocol.Ssh.IntegrationTests` csproj (the stash's project reference), `ProtocolIsolationTests.cs` (the new library joins ADR-0120's table; without it ProtocolLibrary_References_OnlyAbstractionsAndHandBuiltLibraries failed) and ADR-0120 (its table row). No task in Doing names them.
- Verified on this exact work tree: `dotnet build Curl.slnx` 0 warnings 0 errors; full fast tests green (112 assemblies; Curl.Ssh.UnitTests 431, Curl.Protocol.Ssh.UnitTests 1511, Curl.Conformance.SshServer.UnitTests 139) apart from the isolation test, then fixed and its class rerun green (41); `Measure-CodeQuality.ps1 -Library Curl.Ssh.UnitLibrary,Curl.Protocol.Ssh.UnitLibrary,Curl.Conformance.SshServer.UnitLibrary`: 0 failing members across the 3 assemblies; `dotnet format whitespace --verify-no-changes` clean on the changed projects. Added ADR-1162, `Curl.Ssh.UnitLibrary/CLAUDE.md`, and the protocol library's CLAUDE.md now says where the transport lives. Next claim: build, run the fast tests once, move to Done and commit; no quality rerun needed unless the code changed.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: e09ab28e 112a1330 10174c6c e35aff77 3d836d5e.
- 2026-10-30: Doing -> Backlog. Criterion 2 reworded: SshWireDecoders stays in Curl.Protocol.Ssh (the audit fuzzer's entry point, ADR-1162); the whole move is done and verified in the stash, so the next claim builds, runs the fast tests and finishes
- 2026-10-30: Backlog -> Deferred. Split into BL-4375 and BL-4376 (over its cost cap)
