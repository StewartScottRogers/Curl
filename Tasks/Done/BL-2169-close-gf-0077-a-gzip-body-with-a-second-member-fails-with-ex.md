---
id: BL-2169
title: Close GF-0077: A gzip body with a second member fails with exit 23 and 'Failed writing received data to disk/application' where curl says 'Multi-member gzip response not supported'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2169 — Close GF-0077: A gzip body with a second member fails with exit 23 and 'Failed writing received data to disk/application' where curl says 'Multi-member gzip response not supported'

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0077 (A gzip body with a second member fails with exit 23 and 'Failed writing received data to disk/application' where curl says 'Multi-member gzip response not supported'), so a later gap analysis measures each of `behaviour:test2305` as `match`.

## Context

- Finding: GF-0077, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Medium. Introduced in: not stated upstream.
- Items: `behaviour:test2305`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test2305 (--compressed --silent --show-error, two concatenated gzip members): expected 'reference curl exits 23; stdout 0 bytes', actual '<verify><stderr> differs at byte 11 (line 1): expected curl: (23) Multi-member gzip response not supported, got curl: (23) Failed writing received data to disk/application'. The exit code matches; only the message differs. HttpContentCodingDecoder (Curl.Protocol.Http.UnitLibrary/HttpContentCodingDecoder.cs lines 29 and 188) stops before the second member and fails with the generic write-failure message. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2305

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary, when HttpContentCodingDecoder finds a second gzip member after the first one's trailer, fail with exit 23 and a new HttpTransferMessages constant 'Multi-member gzip response not supported', as curl 8.22.0 does; keep 'Failed writing received data to disk/application' for other trailing bytes (zlib, Brotli) after checking each against the reference.

## Acceptance criteria

- [x] `behaviour:test2305`: Curl answers what curl 8.22.0 answers, `reference curl exits 23; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured curl 8.22.0 (mingw, Schannel, zlib 1.3.2) with Record-CurlExchange.ps1,
  `--compressed --silent --show-error`, a 27-byte gzip member of `Hello, ` followed by:
  a second member (test2305's body) -> exit 23 `Multi-member gzip response not supported`;
  just `1F 8B` -> the same; a lone `1F` -> exit 23 `Failed writing received data to
  disk/application`; `junk` -> the generic message. Every case wrote the first member's
  7 bytes to stdout first. `--tr-encoding` with `Transfer-Encoding: gzip` and a second
  member gives the multi-member message too.
- Fix: `HttpContentCodingDecoder.FindEnd` now throws the new
  `HttpTransferMessages.MultiMemberGzipNotSupported` when the bytes after a gzip member's
  end, in the same read, start with `1F 8B`; everything else after the end keeps the
  generic message. Bytes that arrive in a later read than the member's end keep the
  generic message (default taken): curl's check looks only at the bytes left in the
  buffer being inflated (a lone `1F` gets the generic text), and a split read cannot be
  measured with the one-send recorder, so the decoder does not hold trailing bytes to
  look ahead.
- Tests: `HttpContentCodingDecoderTests.Decode_SecondGzipMemberInTheSameRead_...` and
  `HttpProtocolHandlerTests.ExecuteAsync_DecodedBodyWithASecondGzipMember_...` (TE and CE);
  the old second-member rows that pinned curl 8.21.0's generic message moved there, and a
  `1F 41` row covers the second magic byte failing.
- No option changed, so `--ai-help` needs nothing.
- Measure-CodeQuality.ps1 not run (30-45 minutes while lanes build): every branch of the
  new `StartsAnotherGzipMember` is reached by a test row (zlib stream, lone 1F, 41 42,
  1F 41, 1F 8B).

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. A gzip body with a second member in the same read exits 23 with 'Multi-member gzip response not supported', as curl 8.22.0 does (test2305)
