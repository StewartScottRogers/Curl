---
id: BL-1820
title: Close GF-0027: -C N --fail against a 416 whose Content-Range says the file is complete exits 22 instead of 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1820 — Close GF-0027: -C N --fail against a 416 whose Content-Range says the file is complete exits 22 instead of 0

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0027 (-C N --fail against a 416 whose Content-Range says the file is complete exits 22 instead of 0), so a later gap analysis measures each of `behaviour:test194` as `match`.

## Context

- Finding: GF-0027, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test194`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test194 (-C 87 --fail; reply 416 with 'Content-Range: bytes */87') expected 'upstream test194 passes' (the reference curl exits 0), actual: <verify><errorcode>: expected exit code 0, got 22. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 194

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's HttpDownloadConditions / HttpContentRange: treat a 416 whose 'Content-Range: bytes */<size>' equals the -C offset as 'the file is already complete'. Answer it before the -f check, with exit 0 and no body written, as curl 8.21.0 does.

## Acceptance criteria

- [x] `behaviour:test194`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Cause: curl 8.21.0's `http_should_fail` excuses a 416 to a resumed GET (`resume_from` set, `httpreq == HTTPREQ_GET`) from `-f`/`--fail-with-body` whatever its Content-Range says; Curl failed it with exit 22 before `HttpDownloadConditions.Decide` could answer it as `NothingLeftToResume`.
- Fix: `HttpDownloadConditions.IsResumeAlreadyComplete` (416, `-C` > 0, not `-I`, no request body) and `HttpProtocolHandler.FailModeOf` drops the fail mode for it, so the existing 416 path writes no body and exits 0. No option changed, so `--ai-help` is unaffected.
- Tests: `HttpDownloadConditionsTests.IsResumeAlreadyComplete_OnlyA416ToAResumedGet` (5 rows, every branch) and `HttpProtocolHandlerTests.ExecuteAsync_ResumeAnswered416UnderFail_SucceedsWithNoBody` (test194's reply, every chunk size). Fast tests green on Windows; Measure-CodeQuality not run (both new branches are reached by the new rows), to stay inside the run's budget.
- No ADR: the behaviour is curl's, read from its source, not a choice.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. -C N --fail against a 416 now exits 0 with no body, as curl 8.21.0 does (test194)
