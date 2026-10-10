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
completed:
---
# BL-1935 — Run curve25519-sha256 key exchange, NEWKEYS and the service request in the conformance SSH server

## Goal

The conformance SSH server completes key exchange with Curl's SSH client up to the `ssh-userauth` service request: `KEXINIT`, `curve25519-sha256`, a fixed Ed25519 host key, the cipher and MAC the client offers first, `NEWKEYS`, `SERVICE_ACCEPT`.

## Context

Part 2 of BL-1899; builds on BL-1934's scaffold. Read ADR-0456. Reuse `Curl.Protocol.Ssh.UnitLibrary`'s `Negotiation/SshKexInit.cs`, `SshAlgorithmNegotiator.cs`, `KeyExchange/X25519SshKeyShare.cs`, `SshExchangeHashInput.cs`, `SshKeyDerivation.cs` and `PacketProtection/SshPacketProtections.cs` through `InternalsVisibleTo`. The ungated server-side reference is `Curl.Protocol.Ssh.UnitTests\Fakes\InMemorySshServerSession.cs` and `TestKeyExchangeServer.cs`; the host key is RFC 8032 section 7.1's first Ed25519 key, as in `Fakes\TestHostKey.cs` (`TestHostKey.Ed25519`). No authentication yet (`ssh-userauth` is accepted, nothing after it).

## Acceptance criteria

- [ ] A unit test connects Curl's own `SshTransport` (Curl.Protocol.Ssh.UnitLibrary, in memory) to the server and completes `NegotiateAlgorithmsAsync`, `ExchangeKeysAsync`, `NEWKEYS` and the `ssh-userauth` service request, encrypted after `NEWKEYS`.
- [ ] The server exposes its host key blob and its MD5 (hex) and SHA-256 (base64, no padding) fingerprints as constants that are the same on every run and platform; a test pins both values, and the library's `CLAUDE.md` says how a `--hostpubmd5`/`--hostpubsha256` case names them.
- [ ] The server chooses the client's first offered cipher and MAC that `SshPacketProtections` implements; tests cover `aes128-ctr` with `hmac-sha2-256`, and `chacha20-poly1305@openssh.com`.
- [ ] 100% line and branch coverage, complexity at most 10; `dotnet build -warnaserror` clean and `dotnet test --filter "TestCategory!=Integration"` green.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
