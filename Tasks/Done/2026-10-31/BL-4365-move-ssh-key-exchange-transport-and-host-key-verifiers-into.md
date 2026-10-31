---
id: BL-4365
title: Move SSH key exchange, transport and host key verifiers into Curl.Ssh.UnitLibrary
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320, BL-4375, BL-4376, BL-4363, BL-4364]
touches: [Curl.Ssh.UnitLibrary, Curl.Ssh.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests/Transport, Curl.Protocol.Ssh.UnitTests/KeyExchange, Curl.Protocol.Ssh.UnitTests/HostKeys, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.Transport.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.KeyExchange.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.HostKeys.cs, Curl.Protocol.Ssh.UnitTests/Fakes, Curl.slnx, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests, Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.PlatformBuilds.cs, Curl.Engine.UnitTests/EngineCompositionPlatformLibraryTests.cs]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4365 — Move SSH key exchange, transport and host key verifiers into Curl.Ssh.UnitLibrary

## Goal

The Transport and KeyExchange folders, the host key signature verifiers and the transport log live in Curl.Ssh.UnitLibrary, and Curl.Protocol.Ssh.UnitLibrary no longer holds any of the five transport folders.

## Context

Part 4 of BL-4328, the last. Move Transport, KeyExchange, the HostKeys signature verifiers (ISshSignatureVerifier, SshSignatureVerifiers, Dsa/Ecdsa/Ed25519/Rsa/OpenSshCertificate/SecurityKey* verifiers, SshKeyBlobReader, SecurityKeySignedData) into namespace Curl.Ssh.HostKeys, and the transport log from BL-4363, with their tests; known_hosts files stay in the protocol library. Update using lines in the conformance SSH server, Curl.Console, Curl.Engine and their tests. Verify the whole layer in the ADR from BL-4362.

## Acceptance criteria

- [x] Transport, KeyExchange, HostKeys signature verifiers and the transport log and their tests live only in `Curl.Ssh.UnitLibrary` / `Curl.Ssh.UnitTests` (namespaces `Curl.Ssh.<Folder>`).
- [x] `Curl.Ssh.UnitLibrary` references no `Curl.Protocol.*` library other than `Curl.Protocol.Abstractions.UnitLibrary`; `Curl.Protocol.Ssh.UnitLibrary` references it and holds none of the five transport folders; every existing SSH unit and integration test passes unchanged in behaviour.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-30 (interactive): nothing was left once BL-4375 and BL-4376 landed BL-4362's move. Transport, KeyExchange, Negotiation, Compression and PacketProtection and their tests are in Curl.Ssh.UnitLibrary/UnitTests with no `Curl.Protocol.Ssh.<Folder>` namespace left; the signature verifiers are in Curl.Ssh.UnitLibrary/HostKeys, tested through Curl.Ssh.UnitTests/Transport; known-hosts checking stays in Curl.Protocol.Ssh as curl's policy. Curl.Ssh references only Cryptography and Protocol.Abstractions. BL-4376 measured the three SSH libraries at 0 failing gates; the build is clean and 112 fast-test assemblies passed at 2b5b4064e.

- 2026-10-30 (BL-4367): `touches` narrowed from the whole Curl.Protocol.Ssh.UnitTests to the files and folders this task changes there. That project now imports each moving namespace once, in `GlobalUsings/Curl.Protocol.Ssh.<Folder>.cs`, so moving a folder changes that one file (to `global using Curl.Ssh.<Folder>;`) rather than the using lines of every test that names its types; root types moved to Curl.Ssh get their own new `GlobalUsings/Curl.Ssh.cs`.

- 2026-10-30: Split from BL-4328 (over its cost cap); see its Notes for the dependency closure measured. Each moved folder's namespaces become `Curl.Ssh.<Folder>`; every moved type is `internal`, so `Curl.Ssh.UnitLibrary` needs `InternalsVisibleTo` for the consumers listed in BL-4328's Notes.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Deferred. Its work is already in BL-4362's stashed change (the whole BL-4328 move, ADR-1162); look again once BL-4362 is Done, when nothing should be left
- 2026-10-30: Deferred -> Backlog. BL-4362's parts are Done; closing
- 2026-10-30: Backlog -> Doing. Interactive close Criteria at claim: 9a77872d 19b48342 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. Work landed with BL-4375 and BL-4376; criteria checked interactively
