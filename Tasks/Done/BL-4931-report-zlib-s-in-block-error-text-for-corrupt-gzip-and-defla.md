---
id: BL-4931
title: Report zlib's in-block error text for corrupt gzip and deflate responses
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContentDecoder.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpContentCodingDecoder.cs, Curl.Original.Protocol.Http.UnitTests/CorruptDeflateMessageTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4931 — Report zlib's in-block error text for corrupt gzip and deflate responses

## Goal

Corrupt deflate data gives curl's zlib message after `Error while processing content unencoding:`.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-021 (Low, confirmed: reproduced at least twice).

- Repro: Server on 18200: `/a` gzip of `hello`x100 truncated to 10 bytes plus `garbagegarbage` (gzip); `/b` `\x1f\x8b\x08\x00garbagegarbage` (gzip); `/c` `\x78\x9c\xff\xff\xffgarbage` (deflate). `curl -sS --compressed http://127.0.0.1:18200/a` (then `/b`, `/c`).
- Expected: REF 8.22.0, exit 61: `/a` `invalid block type`; `/b` `invalid code lengths set`; `/c` `invalid stored block lengths`, each after `curl: (61) Error while processing content unencoding: `.
- Actual: Exit 61 but all three print `curl: (61) Unrecognized or bad HTTP Content or Transfer-Encoding`.
- Where to start: Both decoders fall back to `HttpTransferMessages.BadContentEncoding`; `incorrect header check` and `incorrect data check` already match. The hand inflater must detect the three conditions.

## Acceptance criteria

- [x] `/a`, `/b` and `/c` print REF's three messages with exit 61.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- 2026-11-11 (lane 1): Applied lane 9's work (branch factory/BL-4931-lane-9-20261111-101854; see its notes: measured REF, `/c` reads as raw deflate from byte 0 as curl does, generic pins in Cgr071Tests and HttpContentCodingDecoderTests corrected to the measured text). Fixed the quality miss: `FirstBlockFault`'s switch on two bits left the compiler's unreachable out-of-range edge uncovered, so it is now plain conditions. Measure-CodeQuality.ps1 for Curl.Original.Protocol.Http.UnitLibrary: 0 failing members. Unit tests pin the review's `/b` and `/c` bytes and an equivalent `/a` (gzip header then a reserved block type); platform-neutral (no paths or OS text).

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 9091f8de a0b49a29.
- 2026-11-11: Doing -> Backlog. Lane 9 could not integrate: quality gates missed by 1 member(s) of files it changed: HttpContentCodingDecoder.FirstBlockFault(System.ReadOnlySpan<byte>) (Curl.Original.Protocol.Http.UnitLibrary/HttpContentCodingDecoder.cs: branch), still after a repair run. The work is on branch factory/BL-4931-lane-9-20261111-101854; start with git cherry-pick --no-commit factory/BL-4931-lane-9-20261111-101854 and fix it.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 9091f8de a0b49a29.
- 2026-11-11: Doing -> Done. Corrupt gzip and deflate bodies print zlib's in-block text (invalid block type, invalid code lengths set, invalid stored block lengths) with exit 61
