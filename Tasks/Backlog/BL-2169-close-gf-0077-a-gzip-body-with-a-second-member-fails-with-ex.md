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
completed:
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

- [ ] `behaviour:test2305`: Curl answers what curl 8.22.0 answers, `reference curl exits 23; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-13: Created.
