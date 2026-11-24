---
id: BL-5881
title: Record that the port keeps downloading over blowfish-cbc and cast128-cbc where macOS libssh2 crashes (ADR)
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-blowfish-cast128-macos-libssh2-crash.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-5881 — Record that the port keeps downloading over blowfish-cbc and cast128-cbc where macOS libssh2 crashes (ADR)

## Goal

An ADR records that the port's SFTP and SCP clients keep completing a download negotiated with `blowfish-cbc` or `cast128-cbc` on macOS, and do not copy upstream curl 8.21.0's macOS libssh2 build, which crashes there (exit 139, empty output). This is an accepted known difference.

## Context

From Surl's Phase 7 handoff `Z:\repos\Surl\Documentation\Conformance\CurlPort\Port-Board-Handoff.md`, entry 28 (Surl `Ssh.md` finding SSH-3). Seen on osx-arm64 in Surl's `curl-port-phase-7` run 45412760425 (2026-11-24) at port commit `74f3f45`. The handoff asks the port's owners to settle it.

Command: `curl -sS --hostpubsha256 <surl's ssh-rsa SHA-256> -u tester:secret sftp://…/a.txt` against `surl --allow-weak-ssh-algorithms` offering only that cipher. Both negotiate `curve25519-sha256`, `rsa-sha2-512`, the cipher and `hmac-sha2-256`. Upstream then crashes (exit 139, a SIGSEGV in its libssh2 build), and no login reaches surl. The port logs in and downloads the 12 bytes (`hello world`), exit 0.

Decided by Claude under Stewart's delegation (2026-11-24, while filing): keep the working download. A crash is a defect of one platform's libssh2 build, not documented curl behaviour. No curl document asks for it, and exit 139 is a signal, not a curl exit code, so no script can depend on it. Reproducing it would break the drop-in rule everywhere else. The Linux and Windows upstream builds download over these ciphers. This task writes that ADR and tells Surl so its Phase 7 listing can mark the two cases as an accepted known difference.

## Acceptance criteria

- [ ] An ADR in `Documentation/Planning/Decisions/` (next free number), marked "Decided by Claude under Stewart's delegation", records the decision, the measured evidence above and why.
- [ ] No product code changes.

## Notes

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 28.
