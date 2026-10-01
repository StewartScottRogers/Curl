---
id: BL-861
title: Decode Content-Encoding zstd and advertise zstd in --compressed, superseding ADR-0020
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-860]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console.UnitTests, Documentation/Planning/Decisions, Documentation/Planning/Roadmap.md]
requirement: FR-073
created: 2026-09-29
completed: 2026-09-30
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

- [x] `--compressed` sends `Accept-Encoding: deflate, gzip, br, zstd` on the Schannel build, pinned byte for byte against the measured request, with each other platform's measured header pinned in its own test.
- [x] A `Content-Encoding: zstd` body of `hello zstd\n` writes `hello zstd\n`, exit 0, and two concatenated frames write both decoded, as measured.
- [x] A corrupt zstd body, a truncated one and one with bytes after its last frame each give the measured stdout, stderr text and exit code, from tests named for each case.
- [x] A new ADR supersedes ADR-0020, ADR-0020's status reads `Superseded by ADR-<that number>`, and `Documentation/Planning/Decisions/README.md` indexes both.
- [x] `Curl.Protocol.Http.UnitLibrary` meets the quality gates.

## Notes

- 2026-09-29 (lane 3): `Curl.Console.UnitTests` added to `touches`:
  `CurlCommandRunnerTransferEncodingTests.RunAsync_Compressed_SendsAcceptEncodingAndDecodesTheGzipBody`
  pins `Accept-Encoding: deflate, gzip, br`, so changing the header breaks it unless that
  line changes too. BL-633 (in Doing) touches `Curl.Console.UnitTests`, so the task went
  back to Backlog until BL-633 is done.
- Measured 2026-09-29 with `Record-CurlExchange.ps1`, curl 8.21.0 Schannel (mingw64), `-sS --compressed`,
  hand-built frame `28 B5 2F FD 20 0B 59 00 00` + `hello zstd\n` (single segment, 1-byte
  FCS 11, no checksum, one last raw block of 11 bytes; 20 bytes):
  - Request: `Accept-Encoding: deflate, gzip, br, zstd` (after `Accept: */*`). Linux curl
    8.18.0 OpenSSL (WSL Ubuntu) sends the same four tokens, so one value serves every
    platform; macOS's OpenSSL build (Homebrew curl links brotli and zstd) is cited, not measured.
  - Valid frame (Content-Length 20): stdout `hello zstd\n`, stderr empty, exit 0.
  - Two concatenated frames (Content-Length 40): stdout `hello zstd\nhello zstd\n`, exit 0.
  - Truncated (first 15 bytes, Content-Length 15): stdout `hello ` (what the raw block
    gave), stderr empty, exit 0.
  - Corrupt: bad magic (`29 B5 2F FD ...`) and reserved block type (`5F 00 00` header)
    each give empty stdout, `curl: (61) Unrecognized or bad HTTP Content or Transfer-Encoding\n`, exit 61.
  - Bytes after the last frame (`junk` or four `00` bytes, Content-Length 24): stdout
    `hello zstd\n`, then `curl: (61) Unrecognized or bad HTTP Content or Transfer-Encoding\n`,
    exit 61 - unlike Brotli and gzip's exit 23: libzstd starts a new frame and rejects its
    magic. Linux 8.18.0 gives the same (61, 11 bytes out).
  - Caution: with `Connection: close` in the canned response, the recorder occasionally
    gave exit 18 `end of response with N bytes missing`; without it every run above was
    repeated three times and stable.
- Plan: add `HttpContentCoding.Zstandard` (`zstd`), a `DecodeZstandard` loop in
  `HttpContentCodingDecoder` mirroring `DecodeBrotli` but carrying on across `Done`
  (next frame) and mapping `InvalidData` to exit 61 `BadContentEncoding`; a body ending
  mid-frame is not an error. Change `HttpRequestHeadFormatter.AcceptEncoding`, the
  literal in the Http and Console tests, `HttpProtocolHandler`'s doc comment, and add the
  `ProjectReference` plus the protocol `CLAUDE.md` line.
- 2026-09-30 (lane 1): delivered as planned; ADR-0287 supersedes ADR-0020. The Schannel
  header test is `[OSCondition(Windows)]` and
  `Format_Compressed_OnTheOpenSslBuild_SendsTheSameFourTokens` pins the Linux measurement
  off Windows. zstd tests: `HttpContentCodingDecoderTests.Decode_ZstdFrame_*`,
  `_TwoConcatenatedZstdFrames_*`, `_TruncatedZstdFrame_*`, `_CorruptZstdBody_*`,
  `_BytesAfterTheLastZstdFrame_*`, `_LargeZstdBlock_*`; the `Content-Encoding: zstd`
  mapping in `HttpContentDecoderTests`. `Curl.Protocol.Http.UnitLibrary` measured at 100%
  line and branch coverage (cobertura from the MSTest collector); build clean, fast tests
  green. Choice: fewer than four bytes after the last frame are held as a magic number in
  progress and dropped at the end of the body (not measured; libzstd waits for more input
  the same way).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Console.UnitTests (its --compressed test pins the old Accept-Encoding), which BL-633 in Doing touches; measurements recorded in Notes
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. --compressed sends deflate, gzip, br, zstd and Content-Encoding: zstd decodes through Curl.Zstandard, with curl's exit 61 for corrupt or trailing bytes
