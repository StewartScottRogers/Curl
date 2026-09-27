---
id: BL-431
title: Implement FTP download with login, passive mode and RETR in FtpProtocolHandler
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-431 — Implement FTP download with login, passive mode and RETR in FtpProtocolHandler

## Goal

`FtpProtocolHandler` in `Curl.Protocol.Ftp.UnitLibrary` downloads an `ftp://` file over an `IConnection` as curl 8.21.0 does: greeting, `USER`/`PASS` (anonymous by default), `PWD`, `EPSV`/`PASV`, `TYPE I`, `SIZE`, `RETR`, with curl's exit code and message for a failed login.

## Context

- Found in BL-392 (2026-09-27): `Curl.Protocol.Ftp.UnitLibrary` holds only its `.csproj` and `CLAUDE.md`; no FTP handler exists, and `Curl.Console/ForwardedFtpProtocolHandler.cs` fails every non-proxied `ftp://` transfer with exit 1 `Protocol "ftp" not supported`.
- BL-392 (report the last reply code as `TransferReport.ResponseCode`) and the FTP 4xx retry in `TransferRetrier` (BL-317) wait on this handler.
- Measured on curl 8.21.0, 2026-09-27 (BL-392's Context): a server answering `PASS` with `430` gives `curl: (67) Access denied: 430`.
- Record the command sequence and output with `Record-CurlExchange.ps1` (extend it to serve an FTP control and data channel if needed) before pinning bytes. Follow `Curl.Protocol.Ftp.UnitLibrary/CLAUDE.md`: take `IConnection`, never a `Socket`.
- Registering the handler in `Curl.Console` is separate work (it touches `Curl.Console`); file it as its own task.

## Acceptance criteria

- [ ] `FtpProtocolHandler` serves `ftp`, and unit tests driven by a recorded control and data byte stream pin the command sequence curl 8.21.0 sends for `curl ftp://127.0.0.1:<port>/file.txt` and the body written to the output.
- [ ] A `430` reply to `PASS` ends the transfer with exit 67 (`LoginDenied`) and `Access denied: 430`, pinned in a unit test.
- [ ] `dotnet build` is clean with warnings as errors; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
