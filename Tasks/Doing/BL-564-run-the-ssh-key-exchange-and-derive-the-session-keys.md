---
id: BL-564
title: Run the SSH key exchange and derive the session keys
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-563, BL-739, BL-745]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-564 — Run the SSH key exchange and derive the session keys

## Goal

The transport runs every BCL-backed key exchange BL-560's ADR offers (ECDH on NIST curves per RFC 5656; finite-field `diffie-hellman-group1-sha1`, `group14-sha1`, `group14-sha256`, `group16-sha512`, `group18-sha512` per RFC 4253 and RFC 8268; `diffie-hellman-group-exchange-sha1`/`sha256` per RFC 4419; `curve25519-sha256` is BL-678), verifying `rsa-sha2-256`, `rsa-sha2-512`, `ssh-rsa`, `ssh-dss` and `ecdsa-sha2-nistp*` host-key signatures (`ssh-ed25519` is BL-678), computes the exchange hash and session identifier, verifies the server's signature over it with the host-key algorithm chosen, derives the six keys (RFC 4253 section 7.2), and switches keys on `NEWKEYS`.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-563.
- **BCL only.** `ECDiffieHellman`, `System.Numerics.BigInteger`, `SHA1`/`SHA256`/`SHA384`/`SHA512`, `RSA`, `DSA` and `ECDsa` for signature checks. Whether the server's host key is *trusted* is BL-566; this task checks the signature only. What the BCL lacks (on any CI platform, per BL-669's ADR) is hand-built in `Curl.Cryptography.UnitLibrary` (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Test vectors: fixed client ephemeral keys (injected), a test peer with fixed host keys; cross-check one exchange hash against a value computed independently in the test from the RFC's definition.
- Measure: a server whose signature is bad cannot be staged with OpenSSH; pin curl's exit code for a failed key exchange from BL-563's measurements (a server that closes during KEX) and record it in Notes.

## Acceptance criteria

- [ ] `Curl.Protocol.Ssh.UnitTests` complete a key exchange for each offered method against the in-memory peer, pin the exchange hash and derived keys for fixed inputs, and reject a bad signature and a bad group element with the exit code and message recorded in Notes.
- [ ] Re-keying requested by the server (`KEXINIT` mid-session) is handled or refused as BL-560's ADR states, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
