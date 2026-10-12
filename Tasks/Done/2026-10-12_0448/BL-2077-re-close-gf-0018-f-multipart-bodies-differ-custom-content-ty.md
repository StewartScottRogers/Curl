---
id: BL-2077
title: Re-close GF-0018: -F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2077 — Re-close GF-0018: -F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0018 (-F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "), so a later gap analysis measures each of `behaviour:test277`, `behaviour:test669`, `behaviour:test1133`, `behaviour:test1315` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0018 ([BL-1811, BL-1972]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0018, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test277`, `behaviour:test669`, `behaviour:test1133`, `behaviour:test1315`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test277 (-F name=daniel -H 'Content-Type: text/info'): <verify><protocol> differs at byte 93 (line 5): expected 'Content-Length: 158', got 'Content-Type: text/info'. test669 (-H 'Content-type: multipart/form-data; charset=utf-8'): expected 'Content-Length: 260', got 'Content-type: multipart/form-data; charset=utf-8'. test1133 (quoted file name with ',', ';', '"'): request differs at byte 634 from the reference curl. test1315 (-F 'file=@a,b;type=magic/content,c'): request differs at byte 390 from the reference curl. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 277,669,1133,1315

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary's -F parser and the multipart writer it feeds: merge a user Content-Type into the generated one (keep the user's type, append '; boundary=...', placed after Content-Length, as curl's header order shows). Parse '@a,b,c' with per-file ';type=' as several file parts. Parse a quoted file name holding ',', ';' and '"' and escape it in Content-Disposition as curl 8.21.0 does.

## Acceptance criteria

- [x] `behaviour:test277`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test669`: Curl answers what curl 8.21.0 answers, `upstream test669 passes`, so the item measures `match`.
- [x] `behaviour:test1133`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1315`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-11, lane 2: no product code needed changing. `HttpRequestHeadFormatter.FormContentTypeOf`
  (BL-1811, BL-1972, kept through BL-2061) already sends the `-H` Content-Type with
  `; boundary=` in the generated header's place after `Content-Length`. Run through the
  in-repo upstream harness (`UpstreamCase_RunThroughCurl_HoldsTheRatchet`, which judges
  against each case's `<verify>`), tests 277, 669, 1133 and 1315 all pass on this commit.
  669, 1133 and 1315 were already on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`;
  277 was not, so it is added now and the ratchet holds it. `Curl.Conformance.UnitTests` is
  added to `touches` for that one line; no task in Doing names it.
- The finding's evidence for 277 (`Content-Type: text/info` at line 5, before
  `Content-Length`) is the pre-BL-1811 order, so the gap run measured a Curl that does not
  hold these fixes - the same pattern BL-2074 and BL-2075 recorded. No `--ai-help` change:
  no option changed. If the next gap run still measures these items as differing, an
  interactive session should check which Curl binary `Gap/Tools/Measure-UpstreamCases.cs`
  runs, since lanes may not read `Gap/`.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. Tests 277, 669, 1133 and 1315 pass the in-repo upstream harness; 277 added to the ratchet
