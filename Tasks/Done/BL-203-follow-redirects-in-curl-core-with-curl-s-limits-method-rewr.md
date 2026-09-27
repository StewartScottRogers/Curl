---
id: BL-203
title: Follow redirects in Curl.Core with curl's limits, method rewriting and credential rules
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-159, BL-160]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-203 — Follow redirects in Curl.Core with curl's limits, method rewriting and credential rules

## Goal

A redirect follower in `Curl.Core.UnitLibrary` wraps `ProtocolDispatcher`, follows `TransferReport.RedirectUrl` with curl's limit, method rewriting and credential rules, and returns a merged report.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: `-L --max-redirs 0` exits 47 `curl: (47) Maximum (0) redirects followed`. Default limit 50.
- `ProtocolDispatcher.DispatchAsync(ITransferContext)` is in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Over the limit returns `CurlExitCode.TooManyRedirects` (47) `Maximum (N) redirects followed`.
- [x] POST becomes GET on 301/302/303 unless the matching `--post30x` is set; tests per code.
- [x] Credentials are dropped on a host change unless `--location-trusted`; a redirect to a disallowed protocol exits as measured.
- [x] The merged report carries the redirect count, effective URL and redirect time.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `RedirectFollower` (wraps `ProtocolDispatcher`) and `RedirectPolicy` in `Curl.Core.UnitLibrary`; 33 test methods in `RedirectFollowerTests` and 2 in `RedirectPolicyTests`. Not wired into `Curl.Console` (BL-234) and the options are not parsed yet (BL-190).
- Decision (sensible default, no ADR because `Documentation/` is outside `touches`): the options `-L` needs that `ITransferContext` does not carry (`--max-redirs`, `--post301/302/303`, `--location-trusted`, redirect schemes) live in a Core-local `RedirectPolicy` record passed to `FollowAsync`, rather than widening the Abstractions contract, because only the follower reads them.
- Decision: the merged report keeps the last hop's report and sets `RedirectCount`, `EffectiveUrl` (the redirect URL text as the handler resolved it; `null` when nothing was followed), sums `HeaderSize`, `RequestSize` and `ConnectionCount` over every hop (curl documents `size_header`, `size_request` and `num_connects` as totals across redirects), keeps the last hop's `DownloadSize`, and moves `Timings.Started` back to the first hop's start with `RedirectDuration` = first hop start to last hop start, so every `%{time_*}` includes the redirects, as curl's timing diagram shows.
- Decision: a negative `MaxRedirects` means no limit (curl's `--max-redirs -1`).
- Measured on 2026-09-26 with curl 8.21.0 (x86_64-w64-mingw32, Schannel) via `Record-CurlExchange.ps1 -Port 18203`, the response `HTTP/1.1 <code> X
Location: <loc>
Content-Length: 0
Connection: close

` on every connection:
  - `-sS -L --max-redirs 0 http://127.0.0.1:18203/a`, 301 to `/next`: one `GET /a`; stderr `curl: (47) Maximum (0) redirects followed`, exit 47. `--max-redirs 2` with `-d x=1 -u u:p`: `POST /a` then two `GET /next` (Authorization kept, same host), `Maximum (2) redirects followed`.
  - `-d x=1`, 301/302/303: `POST /a` + `x=1`, then `GET /next` with no body. With `--post301`/`--post302`/`--post303` for the matching code: `POST /next` + `x=1`. 307: `POST /next` + `x=1`.
  - `-X POST -d x=1`, 301: `POST /next` with no body (custom method kept, body dropped). `-X PUT -d x=1`, 303: `PUT /next`, no body.
  - `-T up.txt` (3 bytes `abc`), 303: `PUT /a` + `abc`, then `GET /next`. 301: `PUT /next` + `abc` again (re-send filed as BL-253).
  - `-I`, 303: `HEAD /a`, `HEAD /next`.
  - `-u u:p -H "Authorization: Bearer t" -H "Cookie: a=b" -H "X-K: v" --oauth2-bearer zz`, 302 to `http://localhost:18203/next`: second request carries only `X-K: v`. With `--location-trusted -u u:p -H "Cookie: a=b"`: `Authorization: Basic dTpw` and `Cookie: a=b` both kept. 302 to `http://127.0.0.1:18204/next` (second listener): no `Authorization`, so a port change drops credentials too.
  - Location `file:///C:/Windows/win.ini`, `dict://...`, `scp://...`: `curl: (1) Protocol "file" is disabled (in redirect)` (resp. "dict", "scp"), exit 1, no second request. `foo://127.0.0.1/x` and `ipfs://abc/x`: `curl: (1) The redirect target URL could not be parsed: Unsupported URL scheme`, exit 1. `ftp://` and `HTTPS://` targets are followed (connect attempted).
  - `--max-redirs 0` with Location `file:///x` or `foo://x/`: `Maximum (0) redirects followed`, so the limit is checked before the scheme.
- From libcurl source, not separately measured: credentials are compared with the first URL's host, port and scheme (not the previous hop's), so a chain that returns to the first host sends them again.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports 100% line, 100% branch, 53 members, 0 failing, worst CRAP 10.
- Follow-up filed: BL-253 (re-send a `-T` upload from its start on a hop that keeps PUT).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. RedirectFollower in Curl.Core follows -L redirects with curl 8.21.0's limit (exit 47), POST-to-GET rewriting, credential dropping and redirect-scheme refusals, returning a merged report
