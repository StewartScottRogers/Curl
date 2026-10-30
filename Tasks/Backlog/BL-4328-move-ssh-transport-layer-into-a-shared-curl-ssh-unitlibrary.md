---
id: BL-4328
title: Move SSH transport layer into a shared Curl.Ssh.UnitLibrary
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Ssh.UnitLibrary, Curl.Ssh.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.slnx]
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

## Log

- 2026-10-29: Created.
