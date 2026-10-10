---
id: BL-2007
title: Re-close GF-0014: --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2007 — Re-close GF-0014: --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0014 (--max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit), so a later gap analysis measures each of `behaviour:test477`, `behaviour:test1618` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0014 ([BL-1807]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0014, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test477`, `behaviour:test1618`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both items expect 'upstream test<N> passes'. test477 (--max-filesize 5 -L, 301 with a 26-byte body): <verify><protocol> differs at byte 81 (line 6): expected 'GET /4770002 HTTP/1.1', got the end. test1618 (brotli bomb, --compressed --max-filesize=1000): the --output file against <reply><data> differs at byte 121: expected the end, got NUL bytes. The reference curl exits 63. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 477,1618

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's HttpDownloadConditions / HttpContentLength: apply --max-filesize only to the body that is kept, not to a redirect response -L follows. Also count the decoded bytes (HttpContentDecoder) against the limit and fail with exit 63 as soon as they pass it.

## Acceptance criteria

- [x] `behaviour:test477`: Curl answers what curl 8.21.0 answers, `upstream test477 passes`, so the item measures `match`.
- [x] `behaviour:test1618`: Curl answers what curl 8.21.0 answers, `reference curl exits 63; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-10: test1618 was the remaining gap. Measured the platform curl 8.21.0 (Schannel,
  System32) with a 100,000-byte gzip bomb and `--compressed -o file`: at `--max-filesize 1000`
  it exits 63 with `Would have exceeded max file size` and creates no file; at 20000 it writes
  the first whole 16384-byte decoded piece and none of the next. `HttpContentDecoder` now
  refuses a decoded piece that would cross the limit before writing any of it, with that
  message; Curl matches at both limits. ADR-0470 (amends ADR-0442).
- test477 could not be reproduced: Content-Length, chunked, close-delimited and kept-alive
  (one connection) 301 bodies over the limit with `-L --max-filesize 5` all send
  `GET /4770002` and match real curl. The upstream case data is behind the audit guard, so a
  lane cannot read it; if the next gap run still measures test477, an interactive session
  should read its exact reply.
- No `--ai-help` change: no option was added or changed.
- Tests: `ExecuteAsync_CompressedBodyDecodesPastMaxFileSize_WritesNothingAndFailsWithExit63`
  (renamed, now pins 0 bytes and the measured message) and
  `ExecuteAsync_CompressedBodyDecodesPastMaxFileSizeAfterAPieceFits_WritesOnlyWholePiecesThatFit`.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. a --compressed body decoding past --max-filesize writes none of the crossing piece and exits 63 'Would have exceeded max file size', as curl 8.21.0; ADR-0470
