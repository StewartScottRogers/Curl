---
id: BL-1969
title: Re-close GF-0014: --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1969 — Re-close GF-0014: --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit

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

- [ ] `behaviour:test477`: Curl answers what curl 8.21.0 answers, `upstream test477 passes`, so the item measures `match`.
- [ ] `behaviour:test1618`: Curl answers what curl 8.21.0 answers, `reference curl exits 63; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
