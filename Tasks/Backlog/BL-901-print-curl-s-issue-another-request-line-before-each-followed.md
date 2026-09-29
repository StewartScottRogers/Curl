---
id: BL-901
title: Print curl's Issue another request line before each followed -L redirect
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-901 — Print curl's Issue another request line before each followed -L redirect

## Goal

Under `-v -L`, each redirect that is followed prints `* Issue another request to this URL: '<target>'` right after the hop's `Connection #N to host ... left intact` line, as curl 8.21.0 does; today only the 417 resend prints it.

## Context

- Found by BL-621 (2026-09-29): curl 8.21.0 printed, for `-v -skL https://localhost:18443/` answered `301` with `Location: http://localhost:18443/x`:
  `* Connection #0 to host localhost:18443 left intact` / `* Issue another request to this URL: 'http://localhost:18443/x'` / `* Switched from HTTP to HTTPS due to HSTS => https://localhost:18443/x`.
- The line text is `HttpConnectionInfoLines.IssueAnotherRequest` in `Curl.Protocol.Http.UnitLibrary`; the redirect loop is `Curl.Core.UnitLibrary/RedirectFollower.cs`, which prints the HSTS switch line after it.
- Measure first: whether the line is printed when `--max-redirs` refuses the hop, when the target does not parse, and when `--proto-redir` refuses its scheme.

## Acceptance criteria

- [ ] Measured with `Record-CurlExchange.ps1` for the cases above; stderr copied into Notes.
- [ ] A test pins the line's place for a followed redirect, and `CurlCommandRunnerHstsTests.RunAsync_RedirectToHttpOfAHostJustLearned_IsSwitchedBeforeTheNextHop` asserts the Issue line before the switch line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-09-29: Created.
