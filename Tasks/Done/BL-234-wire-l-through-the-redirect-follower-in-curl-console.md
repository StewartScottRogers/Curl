---
id: BL-234
title: Wire -L through the redirect follower in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-203, BL-179, BL-190, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-234 — Wire -L through the redirect follower in Curl.Console

## Goal

`-L`, `--max-redirs`, `--post30x` and `--location-trusted` drive BL-203's redirect follower from `Curl.Console`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: `-L --max-redirs 0` exits 47 `curl: (47) Maximum (0) redirects followed`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `-L -i` prints every response's headers, as measured.
- [x] `--max-redirs 0` exits 47 with the measured line.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Plan (small enough to skip protocol-architect): `HttpRequestOptionsMapping` copies `-L` into `HttpRequestOptions.FollowRedirects`; new `RedirectPolicyMapping` maps `--max-redirs`, `--post301/302/303` and `--location-trusted` onto `RedirectPolicy`; `CurlCommandRunner` performs every transfer through `new RedirectFollower(dispatch.Dispatcher)` instead of the dispatcher directly. Without `-L` the follower dispatches once, so no other behaviour changes. `--proto-redir` is not parsed yet, so the policy keeps curl's default allowed schemes.
- Measured 2026-09-26 with curl 8.21.0 (`C:\Program Files\Git\mingw64\bin\curl.exe`) against a PowerShell `TcpListener` on 127.0.0.1 serving one canned response per connection (`Record-CurlExchange.ps1` serves only one response, so a two-response variant was run from `obj/`). Responses: `/a` = `HTTP/1.1 302 Found
Location: /b
Content-Length: 3
Connection: close

xyz`, `/b` = `HTTP/1.1 200 OK
Content-Length: 5
Connection: close

hello`.
  - `curl -L -i -s http://127.0.0.1:18244/a`: stdout = the 302 head (no `xyz`) + the 200 head + `hello`; requests `GET /a`, `GET /b`; stderr empty.
  - `curl -L --max-redirs 0 -i http://127.0.0.1:18245/a`: stdout = the 302 head only; stderr ends `curl: (47) Maximum (0) redirects followed`; one request.
  - `curl -i -sS http://127.0.0.1:18247/a` (no `-L`): stdout = the full 302 including `xyz`; one request.
  - `curl -L -sS --max-redirs 1` with both hops answering the 302: stdout empty, stderr `curl: (47) Maximum (1) redirects followed`, two requests.
  - Pinned in `CurlCommandRunnerRedirectTests`.
- Without `-s`, curl writes a meter status line per hop and the meter before the exit-47 line; the runner does not model that yet. Filed as BL-277.
- `Measure-CodeQuality.ps1` flagged three members of `TransferDispatch` (the record's generated copy constructor and init setters, from BL-072) as uncovered; turned it into a sealed class with get-only properties, which has no such members. Curl.Console now measures 100% line and branch, 0 failing members.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -L, --max-redirs, --post30x and --location-trusted now drive RedirectFollower from Curl.Console; -L -i prints every head and --max-redirs 0 exits 47 as measured
