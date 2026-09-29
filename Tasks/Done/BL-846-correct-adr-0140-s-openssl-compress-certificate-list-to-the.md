---
id: BL-846
title: Correct ADR-0140's OpenSSL compress_certificate list to the captured zlib and zstd
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0140-the-hand-built-tls-client-runs-quic-always-and-tcp-only-where-sslstream-cannot.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-846 — Correct ADR-0140's OpenSSL compress_certificate list to the captured zlib and zstd

## Goal

ADR-0140's decoded OpenSSL 3.5.5 hello says what its captured bytes say about `compress_certificate`.

## Context

- ADR-0140's decoded list for OpenSSL 3.5.5 reads "`compress_certificate` (27: zlib, brotli, zstd)", but the raw capture in the same ADR ends `001b 0005 04 0001 0003`: two algorithms, zlib (1) and zstd (3), no brotli.
- BL-787 built `ClientHelloProfile.OpenSsl` from the bytes (`[0x0001, 0x0003]`) and its test rebuilds the capture byte for byte, so the code is right and only the prose is wrong.

## Acceptance criteria

- [x] ADR-0140's OpenSSL 3.5.5 extension list reads `compress_certificate` (27: zlib, zstd), matching the raw capture.

## Notes

- Checked the raw capture's tail: `001b 0005 04 0001 0003` is a 4-byte algorithm list of zlib (1) and zstd (3). Only the decoded prose changed; no `.cs` or project file was touched, so the build and tests are unaffected.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0140's OpenSSL 3.5.5 compress_certificate list reads zlib, zstd, matching the raw capture
