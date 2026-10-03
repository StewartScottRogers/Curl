---
id: BL-1352
title: Write curl's 'Switch to GET because of N response' and 'Stick to POST instead of GET' -v lines when a redirect changes or keeps the method
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: FR-088
created: 2026-10-03
completed:
---
# BL-1352 — Write curl's 'Switch to GET because of N response' and 'Stick to POST instead of GET' -v lines when a redirect changes or keeps the method

## Goal

`RedirectFollower` reports curl 8.21.0's `http_switch_to_get` info lines right after `Issue another request to this URL: '...'`: `Switch to GET because of <code> response` when `--follow` switches a non-GET request to GET, and `Stick to <method> instead of GET` when `-L` keeps a `-X` method that curl would otherwise have switched.

## Context

- Today `Curl.Core.UnitLibrary/RedirectFollower.cs` reports `IssueAnotherRequestMessagePrefix + target` (line 378) and switches or keeps the method (`DropsCustomMethod`, line 459; `HopMethod`, line 462; `RedirectPolicy.DropsCustomMethodOnSwitchToGet`, set for `--follow`, BL-627), but writes neither line; neither text exists in the solution.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/http.c`:
  - `http_switch_to_get`, lines 1123-1138: when there is a custom request (`-X`) or the request is not a GET, and the follow mode is `CURLFOLLOW_OBEYCODE` (`--follow`): `infof(data, "Switch to GET because of %d response", code)`; else, when there is a custom request and the mode is not `CURLFOLLOW_FIRSTONLY` (so under `-L`): `infof(data, "Stick to %s instead of GET", req)`.
  - It is called for a 301 (line 1322) and a 302 (line 1344) only when the request is a POST (`HTTPREQ_IS_POST`) and `--post301` / `--post302` was not given, and for every 303 (line 1355) unless the request is a POST and `--post303` was given.
  - `Curl_http_follow` writes `Issue another request to this URL` (line 1276) before this switch, so the new line follows it.
- Measured 2026-10-03 with the installed curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -Connections 2 -Response 'HTTP/1.1 303 See Other\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n'`, `--max-redirs 1` (exit 47 after the second hop):
  - `-sv -X POST --follow`: `> POST /`, `* Issue another request to this URL: 'http://127.0.0.1:PORT/b'`, `* Switch to GET because of 303 response`, `> GET /b`.
  - `-sv -d x -X POST -L` and `-sv -X POST -L`: `* Issue another request ...`, `* Stick to POST instead of GET`, `> POST /b`.
  - `-sv -d x -L`: `* Issue another request ...`, `> GET /b`, no line.
  - with a 302 instead, `-sv -d x --follow`: `* Issue another request ...`, `* Switch to GET because of 302 response`, `> GET /b`.
  - `-sv -X PUT -L` against a 302: no line (`PUT` is not a POST, so `http_switch_to_get` is not called).

## Acceptance criteria

- [ ] Tests in `Curl.Core.UnitTests` drive `RedirectFollower` with the fake dispatcher the existing redirect tests use and pin, each as the info line right after `Issue another request to this URL: '...'`: `--follow` (`DropsCustomMethodOnSwitchToGet = true`) with `-X POST` and a 303 gives `Switch to GET because of 303 response`; `--follow` with a POST body and a 302 gives `Switch to GET because of 302 response`; `-L` with `-X POST` and a 303 gives `Stick to POST instead of GET`.
- [ ] Tests pin no line for: `-L` with a POST body and no `-X` on a 303; `-X PUT -L` on a 302; a POST under `--post302` on a 302; a GET on a 301.
- [ ] Every existing redirect test passes unchanged except where it pins the full info-line sequence of one of the cases above, which gains the line.
- [ ] `dotnet build Curl.Core.UnitTests -warnaserror` is clean; `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
