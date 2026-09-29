---
id: BL-861
title: Decode Content-Encoding zstd and advertise zstd in --compressed, superseding ADR-0020
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-860]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions, Documentation/Planning/Roadmap.md]
requirement: FR-073
created: 2026-09-29
completed:
---
# BL-861 — Decode Content-Encoding zstd and advertise zstd in --compressed, superseding ADR-0020

## Goal

`--compressed` sends the platform curl's `Accept-Encoding` (curl 8.21.0 Schannel: `deflate, gzip, br, zstd`) and a `Content-Encoding: zstd` body is decoded through `Curl.Zstandard.UnitLibrary`'s `ZstandardDecoder`, with the exit codes and messages curl gives for a corrupt, truncated or trailing-garbage zstd body.

## Context

- ADR-0020 drops `zstd` "until a zstd decoder exists"; ADR-0185 (BL-785) places that decoder in `Curl.Zstandard.UnitLibrary`, which ADR-0120 (as amended) lets `Curl.Protocol.Http.UnitLibrary` reference. Write a new ADR superseding ADR-0020 and mark ADR-0020 `Superseded by ADR-NNNN`; update `Documentation/Planning/Roadmap.md`'s "Hand-written zstd decoder" line.
- `HttpRequestHeadFormatter.AcceptEncoding` holds today's value; `HttpContentCodingDecoder` drives Brotli with `BrotliDecoder` and its `OperationStatus`, which `ZstandardDecoder` mirrors (`Done` at each frame end; `LastError` on `InvalidData`).
- Measure first with `Record-CurlExchange.ps1` (extend it to serve a zstd body if needed; no Python): the header curl 8.21.0 sends, and stdout, stderr and exit code for a valid zstd body, two concatenated frames, a truncated body, a corrupt body, and bytes after the last frame. Measure or cite what the Linux and macOS OpenSSL builds advertise; pin each platform's answer in its own test if they differ (root `CLAUDE.md`, platform-neutral tests).
- The protocol library's `CLAUDE.md` names the hand-built libraries it references (ADR-0120, "Documentation"); add `Curl.Zstandard.UnitLibrary` there along with the `ProjectReference`.
- FR-073 already states the four-token header.

## Acceptance criteria

- [ ] `--compressed` sends `Accept-Encoding: deflate, gzip, br, zstd` on the Schannel build, pinned byte for byte against the measured request, with each other platform's measured header pinned in its own test.
- [ ] A `Content-Encoding: zstd` body of `hello zstd\n` writes `hello zstd\n`, exit 0, and two concatenated frames write both decoded, as measured.
- [ ] A corrupt zstd body, a truncated one and one with bytes after its last frame each give the measured stdout, stderr text and exit code, from tests named for each case.
- [ ] A new ADR supersedes ADR-0020, ADR-0020's status reads `Superseded by ADR-<that number>`, and `Documentation/Planning/Decisions/README.md` indexes both.
- [ ] `Curl.Protocol.Http.UnitLibrary` meets the quality gates.

## Notes

## Log

- 2026-09-29: Created.
