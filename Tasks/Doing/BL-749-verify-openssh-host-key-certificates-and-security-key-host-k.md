---
id: BL-749
title: Verify OpenSSH host-key certificates and security-key host keys in the SSH transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-566, BL-678]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-749 — Verify OpenSSH host-key certificates and security-key host keys in the SSH transport

## Goal

The SSH transport accepts and verifies every host-key algorithm in ADR-0122's lists that BL-564 and BL-678 do not: the OpenSSH certificate forms (`rsa-sha2-512-cert-v01@openssh.com`, `rsa-sha2-256-cert-v01@openssh.com`, `ssh-rsa-cert-v01@openssh.com`, `ecdsa-sha2-nistp256/384/521-cert-v01@openssh.com`, `ssh-ed25519-cert-v01@openssh.com`, `sk-ecdsa-sha2-nistp256-cert-v01@openssh.com`, `sk-ssh-ed25519-cert-v01@openssh.com`) and the security-key forms (`sk-ecdsa-sha2-nistp256@openssh.com`, `sk-ssh-ed25519@openssh.com`), with the certificate's signature key checked against `@cert-authority` known-hosts entries as curl 8.21.0's libssh2 build treats them.

## Context

- ADR-0122: the certificate forms are in both reference builds' default host-key lists (measured 2026-09-28); the `sk-` forms come from libssh 0.12.2 and are in the full set only.
- Specifications: OpenSSH `PROTOCOL.certkeys` and `PROTOCOL.u2f` (security-key signatures carry a flags byte and counter after the signature; verification uses the BCL's `ECDsa` or BL-672's Ed25519 over SHA-256 of the application and the data).
- Builds on BL-566's known-hosts matcher.

## Acceptance criteria

- [ ] Each certificate and `sk-` host-key algorithm verifies a signature over the exchange hash from ADR-0122's in-memory SSH peer, pinned by a test per algorithm.
- [ ] A certificate whose signature fails, whose validity window has passed (by the injected `TimeProvider`) or whose principals do not name the host is refused with the exit code and message curl 8.21.0 gives, measured with `Record-CurlExchange.ps1` first.
- [ ] `dotnet build` clean, fast tests green, 100% line and branch coverage of the new code.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
