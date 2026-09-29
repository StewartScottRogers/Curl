---
id: BL-903
title: Fail FTP over IPv6 with exit 8 when EPSV is refused instead of falling back to PASV
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-903 — Fail FTP over IPv6 with exit 8 when EPSV is refused instead of falling back to PASV

## Goal

When the control connection is IPv6 and `EPSV` is answered with anything but `229`, the FTP handler ends with exit 8 (`CURLE_WEIRD_SERVER_REPLY`) and `Failed EPSV attempt, exiting`, without sending `PASV`, as curl 8.21.0 does.

## Context

- Found in BL-662. Measured with `Record-CurlExchange.ps1 -Ftp -ListenAddress ::1 -FtpReply 'EPSV=500 no'` and `curl -sS -g ftp://[::1]:<port>/f.txt`: stderr `curl: (8) Failed EPSV attempt, exiting`, exit 8, last commands `PWD`, `EPSV`. With `--disable-epsv` curl sends `PASV` on IPv6 and downloads (exit 0).
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`, `OpenPassiveDataConnectionAsync`, which today falls back to `PASV`. Whether the control connection is IPv6 must come from the connection (a host name can resolve to `::1`), not only from a bracketed URL host; if `IConnection` cannot tell, that needs an Abstractions task first.
- Measure whether curl sends `QUIT` first, and what `-v` prints (`Failed EPSV attempt. Disabling EPSV` is the IPv4 line).

## Acceptance criteria

- [ ] Measured first: the case above with `-v`, with a host name resolving to `::1`, and with `--disable-epsv`; commands, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin the exit 8 case and that IPv4 still falls back to `PASV`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
