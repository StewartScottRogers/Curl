---
id: BL-1816
title: Close GF-0023: -w '\n' written to stdout on Windows ends in CR LF where upstream expects LF
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1816 — Close GF-0023: -w '\n' written to stdout on Windows ends in CR LF where upstream expects LF

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0023 (-w '\n' written to stdout on Windows ends in CR LF where upstream expects LF), so a later gap analysis measures each of `behaviour:test1341` as `match`.

## Context

- Finding: GF-0023, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1341`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1341 (-J -O -D - -w 'curl saved to filename %{filename_effective}\n') expected 'upstream test1341 passes', actual: <verify><file2> stdout1341 differs at byte 342 (line 9): expected '... name1341\n', got '... name1341\r\n'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1341

Suggestion, copied from the finding:

In Curl.Output.UnitLibrary / Curl.Console's -w writer, write a -w line feed to stdout as LF, as upstream's <file2> expects. Keep CR LF only for the %output{} file targets ADR-0081 covers, unless a fresh measurement of the Windows reference curl shows CR LF on stdout too. In that case record it in the item's notes for a 'reference-diverges' cross-check.

## Acceptance criteria

- [ ] `behaviour:test1341`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 277 bytes: HTTP/1.1 200 OK\x0D\x0ADate: Tue, 09 Nov 2010 14:49:00 GMT\x0D\x0AServer: test-server/fake\x0D\x0AContent-Length: 6\x0D\x0AConnection: close\x0D\x0AContent-Type: text/html\x0D\x0AContent-Disposition: filename=name1341; charset=funny; op`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
