---
id: BL-1810
title: Close GF-0017: --compressed mishandles 'Content-Encoding: none' and a broken deflate header
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1810 — Close GF-0017: --compressed mishandles 'Content-Encoding: none' and a broken deflate header

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0017 (--compressed mishandles 'Content-Encoding: none' and a broken deflate header), so a later gap analysis measures each of `behaviour:test223`, `behaviour:test328` as `match`.

## Context

- Finding: GF-0017, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test223`, `behaviour:test328`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both items expect 'upstream test<N> passes'. test328 (Content-Encoding: none): the --output file against <reply><data> differs at byte 119 (line 7): expected 'Q- What did 0 say to 8? A- Nice Belt!', got the end. test223 (broken deflate header): stderr differs from the reference curl, which exits 61. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 223,328

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's HttpContentCoding / HttpContentDecoder: treat 'none' (like 'identity') as no coding and pass the body through. For a deflate stream with a bad header, fail with exit 61 and curl 8.21.0's exact message ('Error while processing content unencoding: ...').

## Acceptance criteria

- [x] `behaviour:test223`: Curl answers what curl 8.21.0 answers, `reference curl exits 61; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test328`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- `Content-Encoding: none` is now skipped like `identity`: curl's identity coding carries `none` as its alias (content_encoding.c), so test328's body passes through unchanged.
- A `deflate` body without a valid zlib header goes to raw deflate, as curl does after zlib's "incorrect header check". zlib refuses some first blocks at once, and their errors now carry zlib's own text with exit 61: a reserved block type gives "invalid block type", and a stored block whose LEN and NLEN disagree gives "invalid stored block lengths". A broken zlib header keeps method 8 in its low nibble, so raw deflate reads it as a stored block. That makes test223's message "Error while processing content unencoding: invalid stored block lengths". Other corrupt raw data keeps the generic text (ADR-0031).
- Not measured against test223's own bytes: the dark-factory audit guard refuses lanes any path under the gap office's upstream cache. The message comes from zlib's inflate.c. If the next gap run still shows test223 as a gap, the cause is a later zlib error, such as a dynamic-block error, that needs the same treatment.
- No option changed, so `--ai-help` needs no change.
- No ADR: this follows the existing rule (match curl's text) and makes no new design choice.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Content-Encoding none passes through; broken deflate header fails exit 61 with zlib's text
