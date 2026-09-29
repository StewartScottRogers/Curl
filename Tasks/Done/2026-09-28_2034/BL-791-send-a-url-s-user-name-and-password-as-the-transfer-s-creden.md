---
id: BL-791
title: Send a URL's user name and password as the transfer's credentials without -u
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-791 — Send a URL's user name and password as the transfer's credentials without -u

## Goal

A URL with user information (`http://zz:x@host/`, `ftp://u:p@host/f`) sends that user name and password (percent-decoded) as the transfer's credentials when `-u` gives no user name, with or without a netrc option, as curl 8.21.0 does.

## Context

- Found while doing BL-505: nothing turns `CurlUrl.User`/`Password` into `ITransferContext.Credentials`, so today `http://zz:x@127.0.0.1/` sends no `Authorization` and `ftp://u:p@host/` logs in as `anonymous`. Only under `-n`/`--netrc-optional` does `Curl.Console/NetrcCredentialLookup.cs` fall back to the URL's credentials.
- Measured in BL-505 (curl 8.21.0, mingw, Schannel): `curl -s -S http://b:x@127.0.0.1:<P>/` sends `Authorization: Basic Yjp4` (b:x); `curl -s -S http://zz@127.0.0.1:<P>/` sends `Basic eno6` (zz:). `-u a:b` beats the URL's user information (measured with `-n`: `-u q:r` with `b@` sends q:r).
- The natural home is the same lookup: when `-u` gives no user name, the URL's credentials stand unless a netrc entry replaces them. Check the other schemes that read `Credentials` (FTP, MQTT, the mail schemes, SSH) and redirects (`RedirectFollower` drops credentials to another host).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `http://b:x@`, `http://zz@`, `http://:x@`, `http://a%3Ab@`, `-u q:r http://b:x@`, and `ftp://u:p@` with `-Ftp`; request bytes and exit code copied into Notes.
- [x] `Curl.Console.UnitTests` pin each measured case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with curl 8.21.0 (mingw, Schannel) through `Record-CurlExchange.ps1`, `curl -s -S <args>` against 127.0.0.1:47911 (HTTP) and 47912 (`-Ftp`). Every run exited 0; only the `Authorization` line differs from the plain `GET / HTTP/1.1`, `Host`, `User-Agent: curl/8.21.0`, `Accept: */*` request:

| Arguments | Authorization sent |
| --- | --- |
| `http://b:x@…/` | `Basic Yjp4` (b:x) |
| `http://zz@…/` | `Basic eno6` (zz:) |
| `http://:x@…/` | `Basic Ong=` (:x) |
| `http://a%3Ab@…/` | `Basic YTpiOg==` (a:b:) - decoded user `a:b`, empty password |
| `-u q:r http://b:x@…/` | `Basic cTpy` (q:r) - `-u` with a user name wins |
| `-u :pw http://b:x@…/` | `Basic Yjp4` (b:x) - the URL replaces `-u :pw` whole, password included |
| `-u : http://b:x@…/` | `Basic Yjp4` (b:x) |
| `http://@…/` | none |
| `http://:@…/` | none |
| `-Ftp ftp://u:p@…/f` | `USER u`, `PASS p`, then PWD, EPSV, TYPE I, SIZE f, RETR f, QUIT; stdout `hi` |
| `-Ftp ftp://u%40x@…/f` | `USER u@x`, `PASS ` (empty) |

Plan and what was done:
- `NetrcCredentialLookup` renamed `TransferCredentialLookup` (it now also reads the URL; "say what it does"), with `RunningTransferState.NetrcCredentials` -> `LookedUpCredentials` and `TransferContextFactory`'s parameter to match. With no netrc option and no `-u` user name it returns the URL's percent-decoded user name and password, either empty when absent, or null when both are empty (`http://@`, `http://:@`). The netrc path is unchanged (BL-505's measurements).
- Every scheme reads `ITransferContext.Credentials`, so FTP, the mail schemes, MQTT and SSH pick the URL's credentials up the same way; FTP falls back to `anonymous` only when the context carries none (`FtpSession`).
- Redirects: `RedirectFollower` keeps the credentials to the same host and drops them to another, as for `-u`. A `Location` carrying its own user information is not read yet: filed as BL-814.
- No ADR: every behaviour here was measured, not chosen.
- Tests: `Curl.Console.UnitTests/CurlCommandRunnerUrlCredentialsTests.cs` (HTTP wire bytes for each case; FTP credentials on the context). Curl.Console.UnitTests 1420 passed, 4 skipped (off-Windows); `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. A URL's user name and password are sent as the transfer's credentials when -u gives none, for HTTP, FTP and every scheme
