---
id: BL-627
title: Parse --follow and follow redirects keeping the custom method as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-627 — Parse --follow and follow redirects keeping the custom method as curl does

## Goal

`--follow` follows redirects like `-L` but changes a `-X` custom method on `301`/`302`/`303` the way curl 8.21.0 does (per the HTTP specification, rather than keeping it as `-L -X` does).

## Context

- Conformance audit 2026-09-28, row 21 (Major, S). `--follow` was added in curl 8.16.0 (https://curl.se/docs/manpage.html#--follow; `CurlManual.txt` for 8.21.0).
- Redirects: `Curl.Core.UnitLibrary/RedirectFollower.cs`, `RedirectPolicy.cs`, `Curl.Console/RedirectPolicyMapping.cs`; FR-088 pins the POST-to-GET rule for `-L`.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Connections 2`: `--follow -X PUT -d x` against `301`, `302`, `303`, `307`, and `--follow -X DELETE` against `302`, and the same with `-L`; request bytes copied into Notes.
- [x] `Curl.Core.UnitTests` pin the second request's method and body for each case; `Curl.Cli.UnitTests` pin parsing and `--follow` with `-L` together.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-29 on curl 8.21.0 (mingw, Schannel) with
`Record-CurlExchange.ps1 -Connections 2`, first response `HTTP/1.1 <code> R` with
`Location: /b`, second `200 OK`. Every first request is `<METHOD> /a` with, for `-d x`,
`Content-Length: 1` and `Content-Type: application/x-www-form-urlencoded` and body `x`.
Second request:

| Command | 301 | 302 | 303 | 307 |
| --- | --- | --- | --- | --- |
| `--follow -X PUT -d x` | `GET /b`, no body | `GET /b`, no body | `GET /b`, no body | `PUT /b`, `Content-Length: 1`, body `x` |
| `-L -X PUT -d x` | `PUT /b`, no body | `PUT /b`, no body | `PUT /b`, no body | `PUT /b`, `Content-Length: 1`, body `x` |
| `--follow -X DELETE` | `DELETE /b` | `DELETE /b` | `GET /b` | - |
| `-L -X DELETE` | - | `DELETE /b` | `DELETE /b` | - |

Second request bytes, e.g. case `--follow -X PUT -d x` / 302:
`GET /b HTTP/1.1\r\nHost: 127.0.0.1:18602\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`;
`-L` / 302: the same with `PUT` in place of `GET`; `--follow` and `-L` / 307:
`PUT /b HTTP/1.1\r\nHost: ...\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 1\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx`.

More measured cases:
- `--follow -X PUT -d x` with `--post301` / `--post302` / `--post303`: `PUT /b` with the body again. `--follow -X POST -d x --post303` on 303: `POST /b` with the body. 308: `PUT` with the body.
- `--follow --post303 -X DELETE` on 303: `GET /b` (`--post303` keeps only a body). `--follow -X HEAD` on 303: `GET /b`.
- `--follow -X PUT -T file`: 302 sends `PUT /b` with the file again; 303 sends `GET /b` with nothing.
- `--follow` and `-L` are one switch, the last wins: `--follow -L`, `--follow --location-trusted`, `--follow --no-follow -L`, `--follow --no-location -L`, `--follow --no-location --location-trusted` all behave as `-L` (PUT kept); `-L --follow` and `--location-trusted --follow` behave as `--follow` (GET). `--follow --no-location`, `-L --no-follow` and `--follow --no-location-trusted` do not follow.

Rule implemented: under `--follow`, the `-X` method is dropped (the hop becomes GET) once the
body is dropped (301/302/303 without the matching `--post30x`, or a 303 dropping a `-T`
upload) and on any 303 to a request without a body; once dropped it stays dropped. `-L`
keeps it as before. Parsing: `CommandLineOptions.FollowRedirectsPerSpec`, set by `--follow`
and cleared by `-L`, `--location-trusted` and their `--no-` spellings; mapped to
`RedirectPolicy.DropsCustomMethodOnSwitchToGet`. No ADR: every rule is measured, not chosen.

`RedirectFollower.StopBeforeHop` was at complexity 12 by the coverage tool's count (from
BL-626); its user-in-URL and proxy/rewind checks moved into `UserInUrlFailure` and
`HopProxyOrRewindFailure` so Core has no failing member.

Tests: Cli 2941 passed, Core 1277, Console 1647. In the whole-solution fast run
`ProtocolIsolationTests` (Curl.Protocol.Abstractions.UnitTests) failed twice with
`UnauthorizedAccessException` walking `Curl.Conformance.UnitTests\bin\...\log\test1001-*` while
the Conformance tests delete it - a race between test projects, not this change; the project
passes alone (607/607).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --follow follows redirects and drops a -X method whenever a redirect switches to GET, as curl 8.21.0 does
