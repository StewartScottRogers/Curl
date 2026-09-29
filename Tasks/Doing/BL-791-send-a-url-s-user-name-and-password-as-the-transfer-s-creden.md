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
completed:
---
# BL-791 — Send a URL's user name and password as the transfer's credentials without -u

## Goal

A URL with user information (`http://zz:x@host/`, `ftp://u:p@host/f`) sends that user name and password (percent-decoded) as the transfer's credentials when `-u` gives no user name, with or without a netrc option, as curl 8.21.0 does.

## Context

- Found while doing BL-505: nothing turns `CurlUrl.User`/`Password` into `ITransferContext.Credentials`, so today `http://zz:x@127.0.0.1/` sends no `Authorization` and `ftp://u:p@host/` logs in as `anonymous`. Only under `-n`/`--netrc-optional` does `Curl.Console/NetrcCredentialLookup.cs` fall back to the URL's credentials.
- Measured in BL-505 (curl 8.21.0, mingw, Schannel): `curl -s -S http://b:x@127.0.0.1:<P>/` sends `Authorization: Basic Yjp4` (b:x); `curl -s -S http://zz@127.0.0.1:<P>/` sends `Basic eno6` (zz:). `-u a:b` beats the URL's user information (measured with `-n`: `-u q:r` with `b@` sends q:r).
- The natural home is the same lookup: when `-u` gives no user name, the URL's credentials stand unless a netrc entry replaces them. Check the other schemes that read `Credentials` (FTP, MQTT, the mail schemes, SSH) and redirects (`RedirectFollower` drops credentials to another host).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `http://b:x@`, `http://zz@`, `http://:x@`, `http://a%3Ab@`, `-u q:r http://b:x@`, and `ftp://u:p@` with `-Ftp`; request bytes and exit code copied into Notes.
- [ ] `Curl.Console.UnitTests` pin each measured case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
