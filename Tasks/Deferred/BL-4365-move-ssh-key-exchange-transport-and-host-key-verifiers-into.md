---
id: BL-4365
title: Move SSH key exchange, transport and host key verifiers into Curl.Ssh.UnitLibrary
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320, BL-4362, BL-4363, BL-4364]
touches: [Curl.Ssh.UnitLibrary, Curl.Ssh.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.slnx, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests, Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.PlatformBuilds.cs, Curl.Engine.UnitTests/EngineCompositionPlatformLibraryTests.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4365 — Move SSH key exchange, transport and host key verifiers into Curl.Ssh.UnitLibrary

## Goal

The Transport and KeyExchange folders, the host key signature verifiers and the transport log live in Curl.Ssh.UnitLibrary, and Curl.Protocol.Ssh.UnitLibrary no longer holds any of the five transport folders.

## Context

Part 4 of BL-4328, the last. Move Transport, KeyExchange, the HostKeys signature verifiers (ISshSignatureVerifier, SshSignatureVerifiers, Dsa/Ecdsa/Ed25519/Rsa/OpenSshCertificate/SecurityKey* verifiers, SshKeyBlobReader, SecurityKeySignedData) into namespace Curl.Ssh.HostKeys, and the transport log from BL-4363, with their tests; known_hosts files stay in the protocol library. Update using lines in the conformance SSH server, Curl.Console, Curl.Engine and their tests. Verify the whole layer in the ADR from BL-4362.

## Acceptance criteria

- [ ] Transport, KeyExchange, HostKeys signature verifiers and the transport log and their tests live only in `Curl.Ssh.UnitLibrary` / `Curl.Ssh.UnitTests` (namespaces `Curl.Ssh.<Folder>`).
- [ ] `Curl.Ssh.UnitLibrary` references no `Curl.Protocol.*` library other than `Curl.Protocol.Abstractions.UnitLibrary`; `Curl.Protocol.Ssh.UnitLibrary` references it and holds none of the five transport folders; every existing SSH unit and integration test passes unchanged in behaviour.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-30: Split from BL-4328 (over its cost cap); see its Notes for the dependency closure measured. Each moved folder's namespaces become `Curl.Ssh.<Folder>`; every moved type is `internal`, so `Curl.Ssh.UnitLibrary` needs `InternalsVisibleTo` for the consumers listed in BL-4328's Notes.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Deferred. Its work is already in BL-4362's stashed change (the whole BL-4328 move, ADR-1162); look again once BL-4362 is Done, when nothing should be left
