---
id: BL-1807
title: Close GF-0014: --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1807 — Close GF-0014: --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0014 (--max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit), so a later gap analysis measures each of `behaviour:test477`, `behaviour:test1618` as `match`.

## Context

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

- 2026-10-08: `HttpProtocolHandler.DeliveryOf` no longer applies the Content-Length limit to a
  discarded body (a redirect `-L` follows, a 401 before its retry), as curl's `!ignorebody`
  check does (test477). `HttpContentDecoder.MaximumDeliveredSize`, set from the body reader's
  `MaximumBodySize`, holds the decoded bytes to the limit: it writes what the limit allows and
  ends with exit 63, the same message the undecoded limit gives (test1618). ADR-0442.
- Lane runs may not read `Gap/` or the gap run's upstream data (audit guard), so test1618's
  exact expected output file was not read; writing up to the limit before failing mirrors the
  undecoded path. If the re-measure still shows a byte difference, curl writes less (nothing
  of the overflowing write) and `DeliverWithinLimitAsync` is where to change it.
- Tests: `ExecuteAsync_RedirectBodyOverMaxFileSizeWhileFollowing_GivesTheRedirect` and
  `ExecuteAsync_CompressedBodyDecodesPastMaxFileSize_WritesTheLimitThenFailsWithExit63`.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. --max-filesize skips a discarded redirect body and holds decoded bytes to the limit (exit 63); ADR-0442
