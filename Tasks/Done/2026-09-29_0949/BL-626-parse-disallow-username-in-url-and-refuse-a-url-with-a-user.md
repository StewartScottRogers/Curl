---
id: BL-626
title: Parse --disallow-username-in-url and refuse a URL with a user name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-626 — Parse --disallow-username-in-url and refuse a URL with a user name

## Goal

With `--disallow-username-in-url`, a URL carrying user information (including one reached by a redirect) is refused before any connection with curl 8.21.0's exit code and message.

## Context

- Conformance audit 2026-09-28, row 21 (Major, S).
- URLs are parsed by `CurlUrl` (Abstractions) in `Curl.Console/CurlCommandRunner.cs`; redirects by `Curl.Core.UnitLibrary/RedirectFollower.cs`.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--disallow-username-in-url http://u@127.0.0.1:<P>/`, `http://u:p@...`, `http://:p@...`, and `-L` to a `Location` with user information; stderr, exit code and whether a connection was made copied into Notes.
- [x] `Curl.Console.UnitTests` pin each case; `Curl.Cli.UnitTests` pin parsing.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-29, local curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1` on 127.0.0.1:18626:
  - `-sS --disallow-username-in-url` with `http://u@…/`, `http://u:p@…/`, `http://:p@…/` and `http://@…/`
    (empty user too): exit 67, stderr `curl: (67) URL rejected: Credentials was passed in the URL when prohibited`,
    no connection (request.bin empty). `-s` alone: same exit, empty stderr.
    `-w '[%{num_redirects}|%{url_effective}|%{redirect_url}|%{http_code}|%{num_connects}]'` writes
    `[0|http://u@127.0.0.1:18626/||000|0]`.
  - `-L` to `Location: http://u:p@127.0.0.1:18626/x`: exit 67, the same stderr line, one request only
    (`GET /`), and `-w` writes `[1|http://u:p@127.0.0.1:18626/x||302|1]` - the redirect counts as followed,
    `%{redirect_url}` is empty.
  - `-u x:y` with a plain URL: exit 0. `--disallow-username-in-url --no-disallow-username-in-url` with a user: exit 0.
  - Two URLs, the second with a user: the first transfers, the second fails 67 on its own - per-URL, so the
    option is a per-`--next`-group option (curl keeps it in `OperationConfig`).
- Design: `CommandLineOptions.DisallowUsernameInUrl` (negatable flag); `CurlCommandRunner.ParsedUrlRefusal`
  refuses the first URL right after it parses (before the host-length check); `RedirectPolicy.DisallowsUserInUrl`
  makes `RedirectFollower` refuse a target after the scheme checks and before the hop proxy, counting it followed.
- `touches` widened to `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests` (no task in Doing names them): the
  redirect refusal must report `%{num_redirects}` 1 and `%{url_effective}` the target, which only the follower's
  chain can do; the Console-side hop proxy selector would have reported 0 and the first URL.
- Not matched, filed as BL-910: curl checks user information while parsing, so a URL that both has a user
  and a bad host or port fails 67 in curl (measured: `http://u@127.0.0.1:99999/`) where Curl reports exit 3 first.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --disallow-username-in-url refuses a URL or redirect target with user information with exit 67, as curl 8.21.0 does
