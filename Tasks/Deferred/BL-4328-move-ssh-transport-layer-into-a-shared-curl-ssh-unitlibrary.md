---
id: BL-4328
title: Move SSH transport layer into a shared Curl.Ssh.UnitLibrary
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320]
touches: [Curl.Ssh.UnitLibrary, Curl.Ssh.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.slnx, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests, Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.PlatformBuilds.cs, Curl.Engine.UnitTests/EngineCompositionPlatformLibraryTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4328 — Move SSH transport layer into a shared Curl.Ssh.UnitLibrary

## Goal

A new shared `Curl.Ssh.UnitLibrary` (in `/Wire/Ssh/` in `Curl.slnx`, like `Curl.Zstandard`) holds the SSH transport layer (RFC 4253): the `Transport`, `PacketProtection`, `KeyExchange`, `Negotiation` and `Compression` folders of `Curl.Protocol.Ssh.UnitLibrary`, with `Curl.Protocol.Ssh.UnitLibrary` referencing it and behaving exactly as before.

## Context

Extensions may reference shared layers but no `Curl.Protocol.*` library (ADR-0999, `VerifyExtensionIsolation` in `Directory.Build.props`). BL-4145's `--ssh-jump` extension needs a full SSH client to open direct-tcpip channels through a jump host. About 3,700 lines move with their tests from `Curl.Protocol.Ssh.UnitTests` (same folder names) into `Curl.Ssh.UnitTests`; namespaces become `Curl.Ssh.<Folder>`. Types those folders need from the protocol library's root (`ISshRandomSource`, `SshWireDecoders`, `SshConnectionLostException` and the like) move with them. Use the `new-project` skill. Record the layer in an ADR.

## Acceptance criteria

- [ ] `Curl.Ssh.UnitLibrary` and `Curl.Ssh.UnitTests` exist in `/Wire/Ssh/` in `Curl.slnx`, reference no `Curl.Protocol.*` library other than `Curl.Protocol.Abstractions.UnitLibrary`, and hold the transport, packet protection, key exchange, negotiation and compression code and its tests.
- [ ] `Curl.Protocol.Ssh.UnitLibrary` references `Curl.Ssh.UnitLibrary` and no longer holds those folders; every existing SSH unit and integration test still passes unchanged in behaviour.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29 (lane 5): Filed by BL-4145 (the --ssh-jump extension), which may not reference Curl.Protocol.Ssh.UnitLibrary (ADR-0999), so the SSH client it needs moves into a shared layer first.
- 2026-10-29 (lane 14): First-pass survey, no code edited. Requeued behind BL-4320, which holds `Curl.Console/CurlComposition.cs` (its `using Curl.Protocol.Ssh.Negotiation;` line must become `Curl.Ssh.Negotiation`).
- Touches widened: the moving namespaces are also used by `Curl.Conformance.SshServer.UnitLibrary` (8 files: SshServerClientAccount, SshServerHostKey, SshServerRsaClientKey, SshServerRsaHostKey, SshServerSessionChannel, SshServerSftpProcess, SshServerTransport, SshServerUserAuthentication), `Curl.Conformance.SshServer.UnitTests` (8 test files), `Curl.Console/CurlComposition.cs`, `Curl.Engine.UnitLibrary/EngineComposition.PlatformBuilds.cs` and `Curl.Engine.UnitTests/EngineCompositionPlatformLibraryTests.cs` (each a `using` line). `Curl.Protocol.Ssh.IntegrationTests` uses none of them.
- Dependency closure measured (wider than Context says): the five folders hold 3,738 lines, their tests about 4,980. Beyond them they need: from the root `ISshRandomSource`, `SshWireDecoders`, `SshConnectionLostException`, `Libssh2ErrorCode`, `SshTransferException` (360 lines, uses only Abstractions) and `SshDiagnosticLog`; from `HostKeys` the signature verifiers (`SshTransport` calls `SshSignatureVerifiers.For`, `SshAlgorithmCatalogue` lists `SshSignatureVerifiers.Names`): `ISshSignatureVerifier`, `SshSignatureVerifiers`, the Dsa/Ecdsa/Ed25519/Rsa/OpenSshCertificate/SecurityKey* verifiers, `SshKeyBlobReader`, `SecurityKeySignedData` (about 530 lines, they use only Transport, KeyExchange and Cryptography). Plan: move those verifiers into `Curl.Ssh.HostKeys` (host key signature verification is RFC 4253 section 8); leave known_hosts files in the protocol library.
- `SshDiagnosticLog` (265 lines) mixes transport lines with SFTP (`SftpRequestSent`, `SftpAnswerRead`, uses `Sftp.SftpPacketType`) and known_hosts lines (`HostKeyAccepted(SshOptions, KnownHostsFile?)`), so it must be split: a transport log in Curl.Ssh that `SshTransport` takes, and the SFTP/known_hosts lines kept in the protocol library writing through it. Record that split in the ADR.
- Every moved type is `internal`: `Curl.Ssh.UnitLibrary` needs `InternalsVisibleTo` for Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Protocol.Ssh.IntegrationTests, Curl.Conformance.SshServer.UnitLibrary/UnitTests, Curl.Conformance.UnitProgram.UnitLibrary, Curl.Console, Curl.Engine.UnitLibrary/UnitTests and Curl.Ssh.UnitTests (check which already see `SshAlgorithmCatalogue` today).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: acac56da ea52861d e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Needs Curl.Console/CurlComposition.cs, held by BL-4320 in Doing; depends-on BL-4320
- 2026-10-29: Backlog -> Doing. Criteria at claim: acac56da ea52861d e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-4328-20261029-184521-L7.jsonl
- 2026-10-30: Backlog -> Deferred. Over its cost cap; split into BL-4362, BL-4363, BL-4364, BL-4365
