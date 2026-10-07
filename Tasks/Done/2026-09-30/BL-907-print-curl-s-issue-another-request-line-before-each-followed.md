---
id: BL-907
title: Print curl's Issue another request line before each followed -L redirect
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-907 — Print curl's Issue another request line before each followed -L redirect

## Goal

Under `-v -L`, each redirect that is followed prints `* Issue another request to this URL: '<target>'` right after the hop's `Connection #N to host ... left intact` line, as curl 8.21.0 does; today only the 417 resend prints it.

## Context

- Found by BL-621 (2026-09-29): curl 8.21.0 printed, for `-v -skL https://localhost:18443/` answered `301` with `Location: http://localhost:18443/x`:
  `* Connection #0 to host localhost:18443 left intact` / `* Issue another request to this URL: 'http://localhost:18443/x'` / `* Switched from HTTP to HTTPS due to HSTS => https://localhost:18443/x`.
- The line text is `HttpConnectionInfoLines.IssueAnotherRequest` in `Curl.Protocol.Http.UnitLibrary`; the redirect loop is `Curl.Core.UnitLibrary/RedirectFollower.cs`, which prints the HSTS switch line after it.
- Measure first: whether the line is printed when `--max-redirs` refuses the hop, when the target does not parse, and when `--proto-redir` refuses its scheme.

## Acceptance criteria

- [x] Measured with `Record-CurlExchange.ps1` for the cases above; stderr copied into Notes.
- [x] A test pins the line's place for a followed redirect, and `CurlCommandRunnerHstsTests.RunAsync_RedirectToHttpOfAHostJustLearned_IsSwitchedBeforeTheNextHop` asserts the Issue line before the switch line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-09-30 with `Record-CurlExchange.ps1` against curl 8.21.0 (Schannel, Windows), loopback port 18907, first hop answered `301` with the `Location` shown; the tail of stderr for each case (progress meter lines left out):
  - Followed (`-v -L`, `Location: http://127.0.0.1:18907/x`), exit 0:
    `* Connection #0 to host 127.0.0.1:18907 left intact` / `* Issue another request to this URL: 'http://127.0.0.1:18907/x'` / `* Reusing existing http: connection with host 127.0.0.1`
  - `--max-redirs 0`, exit 47: `* Connection #0 to host 127.0.0.1:18907 left intact` / `* Maximum (0) redirects followed` / `curl: (47) Maximum (0) redirects followed` - no Issue line.
  - Unparsable target (`Location: http://[::1/x`), exit 3: `* Connection #0 ... left intact` / `* The redirect target URL could not be parsed: Bad IPv6 address` / `curl: (3) ...` - no Issue line.
  - `--proto-redir =https`, exit 1: `* Connection #0 ... left intact` / `* Issue another request to this URL: 'http://127.0.0.1:18907/x'` / `* Protocol "http" is disabled (in redirect)` / `curl: (1) ...` - the line comes first.
  - Unknown scheme (`Location: foo://127.0.0.1/x`), exit 1: `* Connection #0 ... left intact` / `* The redirect target URL could not be parsed: Unsupported URL scheme` - no Issue line.
  - `--disallow-username-in-url` (`Location: http://u:p@127.0.0.1:18907/x`), exit 67: `* Connection #0 ... left intact` / `* Issue another request to this URL: 'http://u:p@127.0.0.1:18907/x'` / `* URL rejected: Credentials was passed in the URL when prohibited` - the line comes first, credentials and all.
- Implementation: `RedirectFollower.Refusal` now checks the limit, then that the target parses, then that its scheme is one curl knows, then reports `IssueAnotherRequestMessagePrefix + target + "'"` to the first hop's events, then the HSTS switch, then `--proto-redir`/`--proto`. The text lives as a public constant in `Curl.Core.UnitLibrary` because Core does not reference `Curl.Protocol.Http.UnitLibrary` (whose internal `HttpConnectionInfoLines.IssueAnotherRequest` stays for the 417 and auth resends); `Curl.Protocol.Http.*` were not changed. The target is printed as `%{redirect_url}` holds it, which is how curl prints it.
- Not measured: the multipart rewind failure. The line is reported before it, as for every check after the scheme, since curl rewinds when it sends the next request, after printing the line.
- No ADR: no design choice was left open; the behaviour is curl's, measured above.
- Tests: `RedirectFollowerIssueAnotherRequestTests` (7 cases) pins each measured case; `RedirectFollowerHstsTests` now expects the Issue line before the switch line; `CurlCommandRunnerHstsTests.RunAsync_RedirectToHttpOfAHostJustLearned_IsSwitchedBeforeTheNextHop` asserts it between `left intact` and the switch line. The info-line recorder moved to `Curl.Core.UnitTests/Fakes/RecordingTransferEvents.cs` so both classes share it.
- Verified: `dotnet build Curl.slnx -warnaserror` clean; fast tests all pass (Curl.Core.UnitTests 1297, Curl.Console.UnitTests 1966); `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports 100% line, 100% branch, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. -v -L prints curl's Issue another request line before each followed redirect
