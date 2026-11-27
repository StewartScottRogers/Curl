---
id: BL-6018
title: Fix fitness finding STD-208: corrupt raw or chunked deflate gives the generic message and leaves different partial output (PARTLY FIXED)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6006]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContentDecoder.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpContentCodingDecoder.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpDeflateFaultFinder.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Original.Protocol.Http.UnitTests/HttpContentCodingDecoderTests.cs, Curl.Original.Protocol.Http.UnitTests/HttpContentDecoderTests.cs, Curl.Original.Protocol.Http.UnitTests/HttpDeflateFaultFinderTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6018 — Fix fitness finding STD-208: corrupt raw or chunked deflate gives the generic message and leaves different partial output (PARTLY FIXED)

## Goal

STD-208 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: corrupt raw or chunked deflate gives the generic message and leaves different partial output (PARTLY FIXED). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (Windows 2 runs, Linux 1) · **Area:** HTTP `--compressed`, both platforms.
- **What is fixed:** corrupt zlib-wrapped deflate (not chunked) now matches curl.
- **Mock server:** `/enc?t=rawdeflate&as=deflate&garbage=1` sends a raw-deflate body labelled `Content-Encoding: deflate` with garbage inserted after byte 10; `/enc?t=deflate&garbage=1&chunked=1` sends a corrupt zlib-wrapped body with chunked transfer encoding. The plain text is `The quick brown fox…`.
- **Repro:**
  - `curl -sS --compressed -o o.txt "http://127.0.0.1:24785/enc?t=rawdeflate&as=deflate&garbage=1"`
  - `curl -sS --compressed -o o.txt "http://127.0.0.1:24785/enc?t=deflate&garbage=1&chunked=1"`
- **Expected (REF 8.22 and REF2):** both `curl: (61) Error while processing content unencoding: invalid distance too far back`, exit 61; the first creates no `o.txt`; the second leaves `The ` (4 bytes).
- **Actual (.NET):** both `curl: (61) Unrecognized or bad HTTP Content or Transfer-Encoding`, exit 61; the first creates a 7-byte `o.txt`; the second leaves 8 bytes whose last byte is not in the source text.
- **Likely location:** `Curl.Original.Protocol.Http.UnitLibrary/HttpContentDecoder.cs` / `HttpContentCodingDecoder.cs`, the raw-deflate fallback and chunked paths.
- **Acceptance check:** the first repro prints `invalid distance too far back`, exits 61, and creates no `o.txt`.

## Acceptance criteria

- [ ] the first repro prints `invalid distance too far back`, exits 61, and creates no `o.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

First claim (2026-11-26), code reading only: nothing edited, nothing measured, finding not yet reproduced.

- Requeued because the unit test (second criterion) belongs in `Curl.Original.Protocol.Http.UnitTests`,
  and BL-6006, in Doing, holds that whole project. `touches` now names the test files, the message file
  and a new fault-finder file; no other task in Doing named them apart from BL-6006's project entry.
- Likely cause of the partial output: `HttpContentCodingDecoder.HoldsDecoded` holds decoded bytes only
  for a `ZLibStream` and only before the first hand-out. A raw-deflate fallback (`DeflateStream`) is
  never held, so the single-byte feeds of `FeedLengths` write bytes before the fault (the 7-byte
  `o.txt`); in the chunked case, later pieces are handed out per single-byte feed. curl drops what a
  failing zlib call decoded, so holding per piece for `deflate`, raw too, looks like the fix. Check
  the trailer-check path (`failedCheck`) still writes every decoded byte. Measure gzip before
  changing it.
- Likely cause of the message: `FirstBlockFault` names header faults only, and .NET throws
  `InvalidDataException` for data errors without zlib's text. "invalid distance too far back" needs the
  library's own walk of the Huffman blocks (fixed and dynamic), counting decoded bytes and reporting a
  distance greater than that count. No such inflater exists in the repository (searched the `*.cs`
  files for "too far back" and for an `Inflat` class: no match).
- Real curl here: `C:\Windows\System32\curl.exe` is 8.23.0 (Schannel, zlib 1.3.2, no brotli listed);
  Git for Windows `curl.exe` is 8.21.0.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: f10cf772 69116afe 3d836d5e.
- 2026-11-26: Doing -> Backlog. Waits on BL-6006, which holds Curl.Original.Protocol.Http.UnitTests where this fix's unit test belongs
- 2026-11-26: Backlog -> Doing. Criteria at claim: f10cf772 69116afe 3d836d5e.
