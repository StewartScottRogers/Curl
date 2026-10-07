---
id: BL-1250
title: Fall back to PASV after a failed EPSV data connection, port 0 included, as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1240]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1250 — Fall back to PASV after a failed EPSV data connection, port 0 included, as curl does

## Goal

When the data connection to the port a `229` reply names cannot be made (port 0 included), an IPv4 FTP transfer writes curl 8.21.0's `Failed to connect ... via ...` and `Failed EPSV attempt. Disabling EPSV` `-v` lines, sends `PASV` and carries on, instead of ending with exit 7.

## Context

- Today `FtpSession.ConnectDataAsync` (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`) returns the failed dial as the transfer's exit 7 result, and a `229` naming port 0 fails at once with exit 7 without a dial, because `ConnectTarget.RequirePort` (`Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs`) refuses port 0 (BL-1240).
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Ftp -FtpIdleMilliseconds 3000 -FtpReply 'EPSV=229 Entering Extended Passive Mode (|||0|)' -CurlArgs '-v','ftp://127.0.0.1:18921/f.txt'`: exit 0; commands `USER`, `PASS`, `PWD`, `EPSV`, `PASV`, `TYPE I`, `SIZE f.txt`, `RETR f.txt`, `QUIT`; stderr after the `229`: `* Connecting to 127.0.0.1 port 0` / `*   Trying 127.0.0.1:0...` / `* Immediate connect fail for 127.0.0.1: Address not available` / `* connect to 127.0.0.1 port 0 from 0.0.0.0 port 59177 failed: Address not available` / `* Failed to connect to 127.0.0.1:18921 via 127.0.0.1:0 after 81 ms: Could not connect to server` / `* Failed EPSV attempt. Disabling EPSV` / `> PASV`.
- Same with `(|||40000|` (nothing listening on 40000): `* Connecting to 127.0.0.1 port 40000` / `*   Trying 127.0.0.1:40000...` / `* Failed to connect to 127.0.0.1:18922 via 127.0.0.1:40000 after 2214 ms: Could not connect to server` / `* Failed EPSV attempt. Disabling EPSV` / `> PASV`, exit 0.
- Over IPv6 there is no `PASV` (BL-903); measure what curl does there before deciding.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Ftp.UnitTests` pin that a refused EPSV data dial over IPv4 sends `PASV` next, writes the two `-v` lines above in order, and completes the transfer over the `227`'s port.
- [x] A `229` naming port 0 is dialled (or fails as the dial does) and falls back the same way; `ConnectTarget` accepts port 0 if that is the route taken, with its tests updated.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `FtpSession` splits the passive dial (`DialDataAsync`) from the TLS handshake; a failed EPSV dial over IPv4 writes `Failed EPSV attempt. Disabling EPSV` and sends `PASV`. The connector's own `-v` failure line (rewritten with `via`) comes first, as before. Only a failed dial falls back; a failed TLS handshake on an open data connection still ends the transfer (not measured).
- Port 0 stays undialled, so `ConnectTarget` is unchanged: the session writes the `via` failure line itself and falls back the same way. curl's `Trying 127.0.0.1:0...`, `Immediate connect fail` and `connect to ... failed` lines before it are not reproduced.
- IPv6, measured 2026-10-02 (`Record-CurlExchange.ps1 -Ftp -ListenAddress ::1`, `-g -v ftp://[::1]:18932/f.txt`, 229 naming 40000): exit 8, `-v` `Failed to connect to ::1:18932 via ::1:40000 after 2768 ms: Could not connect to server` then `Failed EPSV attempt, exiting`, no `PASV` or `QUIT`, and the error line keeps the dial's message (curl's error buffer holds the first failf). Implemented that way.
- Two older tests that pinned exit 7 for a failed EPSV dial now use `--disable-epsv` and a `227`, keeping their intent (the connector's failure passes through).
- `--log-level warning` writes `EPSV data connection failed; falling back to PASV`.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A failed EPSV data dial, port 0 included, falls back to PASV over IPv4 with curl's -v lines, and ends with exit 8 over IPv6
