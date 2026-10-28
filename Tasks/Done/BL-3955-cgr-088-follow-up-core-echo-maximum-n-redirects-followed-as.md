---
id: BL-3955
title: CGR-088 follow-up (Core): echo 'Maximum (N) redirects followed' as a -v line when -L hits --max-redirs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr088RedirectLimitTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3955 — CGR-088 follow-up (Core): echo 'Maximum (N) redirects followed' as a -v line when -L hits --max-redirs

## Goal

Under -v and --trace, `-L` with `--max-redirs N` reaching its limit writes `* Maximum (N) redirects followed` as an info line, after the `Connection #0 left intact` line, as curl 8.22.0 does.

## Context

Split from BL-3948 (HTTP part 1), which did the handler paths. Real curl (`Record-CurlExchange.ps1`, `-sv -L --max-redirs 0` against a 302) ends its stderr with `* Connection #0 to host 127.0.0.1:PORT left intact` then `* Maximum (0) redirects followed`. The limit is decided in `RedirectFollower.Refusal` (Curl.Core.UnitLibrary); echo through `first.Events.ReportInfo` there, only for the limit refusal. Run Measure-CodeQuality.ps1 for Curl.Core.UnitLibrary before committing.

## Acceptance criteria

- [x] A focused unit test in Curl.Core.UnitTests/Cgr088RedirectLimitTests.cs fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Reported via `first.Events.ReportInfo` in `RedirectFollower.Refusal`; the existing max-redirs test in RedirectFollowerIssueAnotherRequestTests now expects that line (same file family, same fix). Platform-neutral; Measure-CodeQuality for Curl.Core.UnitLibrary: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 0cf7a71c 779790c0.
- 2026-10-28: Doing -> Done. Limit echoed as info line; tests and quality gate green
