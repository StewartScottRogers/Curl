---
id: BL-1811
title: Close GF-0018: -F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1811 — Close GF-0018: -F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0018 (-F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "), so a later gap analysis measures each of `behaviour:test277`, `behaviour:test669`, `behaviour:test1133`, `behaviour:test1315` as `match`.

## Context

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
- [x] `behaviour:test669`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1133`: split out to BL-1847 (scope change, see Notes); not closed by this task.
- [x] `behaviour:test1315`: split out to BL-1847 (scope change, see Notes); not closed by this task.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Scope split (lane 3, 2026-10-08): test277 and test669 are closed here, in
  `HttpRequestHeadFormatter`. test1133 and test1315 need the `-F` parser plus
  `Curl.Console`'s mapping and `Curl.Core`'s multipart builder, outside this task's touches,
  so they moved to BL-1847 rather than hold this task open; the test1133 and test1315 boxes
  above are carried by BL-1847. GF-0018 closes only when a gap run measures all four.
- Behaviour (curl 8.21.0 `http.c`/`mime.c`): for a `-F` form, curl takes the first `-H`
  `Content-Type` value, appends `; boundary=<boundary>`, sends it as `Content-Type:` in the
  generated header's place after `Content-Length`, and leaves every `-H` `Content-Type` line
  out of the custom headers. A form is recognised by its body type starting
  `multipart/form-data; boundary=`, which only `-F` produces. An empty `-H "Content-Type:"`
  keeps the existing behaviour (no Content-Type sent). Lanes may not read the upstream case
  data under the gap office's folder, so this follows the finding's evidence and curl's source.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. test277 and test669 now send the -H Content-Type merged with the form boundary; test1133 and test1315 split to BL-1847
