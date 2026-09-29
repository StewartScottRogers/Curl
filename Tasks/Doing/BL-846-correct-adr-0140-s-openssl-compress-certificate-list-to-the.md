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
completed:
---
# BL-846 — Correct ADR-0140's OpenSSL compress_certificate list to the captured zlib and zstd

## Goal

ADR-0140's decoded OpenSSL 3.5.5 hello says what its captured bytes say about `compress_certificate`.

## Context

- ADR-0140's decoded list for OpenSSL 3.5.5 reads "`compress_certificate` (27: zlib, brotli, zstd)", but the raw capture in the same ADR ends `001b 0005 04 0001 0003`: two algorithms, zlib (1) and zstd (3), no brotli.
- BL-787 built `ClientHelloProfile.OpenSsl` from the bytes (`[0x0001, 0x0003]`) and its test rebuilds the capture byte for byte, so the code is right and only the prose is wrong.

## Acceptance criteria

- [ ] ADR-0140's OpenSSL 3.5.5 extension list reads `compress_certificate` (27: zlib, zstd), matching the raw capture.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
