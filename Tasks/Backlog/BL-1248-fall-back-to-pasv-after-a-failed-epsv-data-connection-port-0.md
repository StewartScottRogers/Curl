---
id: BL-1248
title: Fall back to PASV after a failed EPSV data connection, port 0 included, as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1240]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1248 — Fall back to PASV after a failed EPSV data connection, port 0 included, as curl does

## Goal

When the data connection to the port a `229` reply names cannot be made (port 0 included), an IPv4 FTP transfer writes curl 8.21.0's `Failed to connect ... via ...` and `Failed EPSV attempt. Disabling EPSV` `-v` lines, sends `PASV` and carries on, instead of ending with exit 7.

## Context

- Today `FtpSession.ConnectDataAsync` (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`) returns the failed dial as the transfer's exit 7 result, and a `229` naming port 0 fails at once with exit 7 without a dial, because `ConnectTarget.RequirePort` (`Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs`) refuses port 0 (BL-1240).
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Ftp -FtpIdleMilliseconds 3000 -FtpReply 'EPSV=229 Entering Extended Passive Mode (|||0|)' -CurlArgs '-v','ftp://127.0.0.1:18921/f.txt'`: exit 0; commands `USER`, `PASS`, `PWD`, `EPSV`, `PASV`, `TYPE I`, `SIZE f.txt`, `RETR f.txt`, `QUIT`; stderr after the `229`: `* Connecting to 127.0.0.1 port 0` / `*   Trying 127.0.0.1:0...` / `* Immediate connect fail for 127.0.0.1: Address not available` / `* connect to 127.0.0.1 port 0 from 0.0.0.0 port 59177 failed: Address not available` / `* Failed to connect to 127.0.0.1:18921 via 127.0.0.1:0 after 81 ms: Could not connect to server` / `* Failed EPSV attempt. Disabling EPSV` / `> PASV`.
- Same with `(|||40000|` (nothing listening on 40000): `* Connecting to 127.0.0.1 port 40000` / `*   Trying 127.0.0.1:40000...` / `* Failed to connect to 127.0.0.1:18922 via 127.0.0.1:40000 after 2214 ms: Could not connect to server` / `* Failed EPSV attempt. Disabling EPSV` / `> PASV`, exit 0.
- Over IPv6 there is no `PASV` (BL-903); measure what curl does there before deciding.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Ftp.UnitTests` pin that a refused EPSV data dial over IPv4 sends `PASV` next, writes the two `-v` lines above in order, and completes the transfer over the `227`'s port.
- [ ] A `229` naming port 0 is dialled (or fails as the dial does) and falls back the same way; `ConnectTarget` accepts port 0 if that is the route taken, with its tests updated.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
