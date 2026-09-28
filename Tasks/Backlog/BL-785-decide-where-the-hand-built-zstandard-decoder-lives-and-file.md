---
id: BL-785
title: Decide where the hand-built Zstandard decoder lives and file its tasks
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-785 — Decide where the hand-built Zstandard decoder lives and file its tasks

## Goal

An ADR decides which library holds a hand-built Zstandard (RFC 8878) decoder used by both HTTP `--compressed` and TLS certificate decompression (RFC 8879), amends ADR-0120's table if it is a new hand-built library, and the tasks that create and build it are filed.

## Context

- ADR-0140 (BL-695): OpenSSL 3.5.5's ClientHello offers `compress_certificate` with zlib, brotli and zstd; the BCL has zlib (`ZLibStream`) and Brotli (`BrotliDecoder`) but no Zstandard. ADR-0020 keeps `--compressed` at `deflate, gzip, br` "until a zstd decoder exists", while curl 8.21.0's mingw build advertises zstd.
- Two consumers in different layers: `Curl.Protocol.Http.UnitLibrary` (content decoding) and `Curl.Tls.UnitLibrary` (BL-786). Protocol libraries may reference only Abstractions and ADR-0120's hand-built libraries, so a shared decoder is a new hand-built library row (for example `Curl.Zstandard.UnitLibrary`, referencing nothing) or an addition to an existing row; the ADR decides and says why.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): hand-built, own `UnitLibrary` and `UnitTests`, never a package.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists, Status Accepted, marked "Decided by Claude under Stewart's delegation", naming the library, what it may reference, its API shape (streaming decode, frame and window limits) and its test vectors (RFC 8878 and the reference implementation's test corpus, cited).
- [ ] If the ADR adds a hand-built library, it amends ADR-0120's table and files the `ProtocolIsolationTests` change with the project-creation task.
- [ ] Tasks are filed on the board for creating the library (if new), building the decoder, and advertising `zstd` in `--compressed` (superseding ADR-0020's limit), and BL-786 is left depending on the decoder task.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
