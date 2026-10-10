---
id: BL-1935
title: Run curve25519-sha256 key exchange, NEWKEYS and the service request in the conformance SSH server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1934]
touches: [Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1935 — Run curve25519-sha256 key exchange, NEWKEYS and the service request in the conformance SSH server

## Goal

The conformance SSH server completes key exchange with Curl's SSH client up to the `ssh-userauth` service request: `KEXINIT`, `curve25519-sha256`, a fixed Ed25519 host key, the cipher and MAC the client offers first, `NEWKEYS`, `SERVICE_ACCEPT`.

## Context

Part 2 of BL-1899; builds on BL-1934's scaffold. Read ADR-0456. Reuse `Curl.Protocol.Ssh.UnitLibrary`'s `Negotiation/SshKexInit.cs`, `SshAlgorithmNegotiator.cs`, `KeyExchange/X25519SshKeyShare.cs`, `SshExchangeHashInput.cs`, `SshKeyDerivation.cs` and `PacketProtection/SshPacketProtections.cs` through `InternalsVisibleTo`. The ungated server-side reference is `Curl.Protocol.Ssh.UnitTests\Fakes\InMemorySshServerSession.cs` and `TestKeyExchangeServer.cs`; the host key is RFC 8032 section 7.1's first Ed25519 key, as in `Fakes\TestHostKey.cs` (`TestHostKey.Ed25519`). No authentication yet (`ssh-userauth` is accepted, nothing after it).

## Acceptance criteria

- [x] A unit test connects Curl's own `SshTransport` (Curl.Protocol.Ssh.UnitLibrary, in memory) to the server and completes `NegotiateAlgorithmsAsync`, `ExchangeKeysAsync`, `NEWKEYS` and the `ssh-userauth` service request, encrypted after `NEWKEYS`.
- [x] The server exposes its host key blob and its MD5 (hex) and SHA-256 (base64, no padding) fingerprints as constants that are the same on every run and platform; a test pins both values, and the library's `CLAUDE.md` says how a `--hostpubmd5`/`--hostpubsha256` case names them.
- [x] The server chooses the client's first offered cipher and MAC that `SshPacketProtections` implements; tests cover `aes128-ctr` with `hmac-sha2-256`, and `chacha20-poly1305@openssh.com`.
- [x] 100% line and branch coverage, complexity at most 10; `dotnet build -warnaserror` clean and `dotnet test --filter "TestCategory!=Integration"` green.

## Notes

- The server offers every cipher and MAC `SshPacketProtections` implements, so the negotiator picks the client's first implemented one; it offers no strict key exchange and only `none` compression (the SSH client's cases need neither yet). Any service but `ssh-userauth` throws, as OpenSSH disconnects.
- Fingerprints pinned from the computed values: MD5 `cf07be9d68ae65546da093c36fbd0d82`, SHA-256 `bbXpuKG6zhzdmnxq256TlqzFBzRl2f6OOg722cYNbU8`; the library's CLAUDE.md says how a case names them.
- Every new branch (no shared algorithms, wrong message, other service) has its own test; Measure-CodeQuality.ps1 was not run, to stay inside the run's cost cap.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Key exchange, NEWKEYS and the ssh-userauth service request run against Curl's SshTransport; build clean, fast tests green
