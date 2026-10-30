---
id: BL-4362
title: Create Curl.Ssh.UnitLibrary and move the shared SSH root types into it
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320]
touches: [Curl.Ssh.UnitLibrary, Curl.Ssh.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.slnx, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests, Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.PlatformBuilds.cs, Curl.Engine.UnitTests/EngineCompositionPlatformLibraryTests.cs]
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

- [ ] `Curl.Ssh.UnitLibrary` and `Curl.Ssh.UnitTests` exist in `/Wire/Ssh/` in `Curl.slnx` and the library references no `Curl.Protocol.*` library other than `Curl.Protocol.Abstractions.UnitLibrary`.
- [ ] ISshRandomSource, SshWireDecoders, SshConnectionLostException, Libssh2ErrorCode and SshTransferException (and their tests) live in `Curl.Ssh.UnitLibrary` / `Curl.Ssh.UnitTests`; `Curl.Protocol.Ssh.UnitLibrary` references the new library and no longer holds them.
- [ ] An ADR records the shared SSH layer and the planned splits, and is listed in the ADR README.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-30: Split from BL-4328 (over its cost cap); see its Notes for the dependency closure measured. Each moved folder's namespaces become `Curl.Ssh.<Folder>`; every moved type is `internal`, so `Curl.Ssh.UnitLibrary` needs `InternalsVisibleTo` for the consumers listed in BL-4328's Notes.
- Continues the stashed work of BL-4328.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: e09ab28e 112a1330 10174c6c e35aff77 3d836d5e.
