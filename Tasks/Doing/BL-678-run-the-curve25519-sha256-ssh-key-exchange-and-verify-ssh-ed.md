---
id: BL-678
title: Run the curve25519-sha256 SSH key exchange and verify ssh-ed25519 host keys
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-564, BL-671, BL-672, BL-668]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-678 — Run the curve25519-sha256 SSH key exchange and verify ssh-ed25519 host keys

## Goal

The SSH transport offers and runs `curve25519-sha256` and `curve25519-sha256@libssh.org` (RFC 8731) in the position BL-560's ADR gives them, and verifies `ssh-ed25519` host-key signatures (RFC 8709) over the exchange hash, using `Curl.Cryptography.UnitLibrary`'s X25519 and Ed25519.

## Context

- Conformance audit 2026-09-28, row 35; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): SSH's Curve25519 and Ed25519 are built by hand, never left out. Builds on BL-564 (key exchange framework, exchange hash, key derivation).
- Primitives: BL-671 (X25519), BL-672 (Ed25519). Reference rule: BL-668 lets `Curl.Protocol.Ssh.UnitLibrary` reference `Curl.Cryptography.UnitLibrary`; add that `ProjectReference` here if no earlier SSH task did, and amend `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md` to name it.
- RFC 8731: shared secret encoded as an mpint, all-zero secret rejected. RFC 8709: `ssh-ed25519` key and signature blob format.
- Test vector: fixed client ephemeral key and a test peer with a fixed Ed25519 host key; cross-check the exchange hash computed independently in the test from RFC 8731's definition.

## Acceptance criteria

- [ ] `Curl.Protocol.Ssh.UnitTests` complete a `curve25519-sha256` exchange against the in-memory peer with an `ssh-ed25519` host key, pinning the exchange hash and derived keys for fixed inputs.
- [ ] An all-zero shared secret, a bad Ed25519 signature and a malformed host-key blob each end the session with the exit code and message BL-564 recorded for a failed key exchange.
- [ ] The `KEXINIT` lists pin the algorithm order BL-560's ADR states, with both names present.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
