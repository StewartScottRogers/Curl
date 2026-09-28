---
id: BL-392
title: Report the FTP server's last reply code as TransferReport.ResponseCode
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-431]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-392 — Report the FTP server's last reply code as TransferReport.ResponseCode

## Goal

An FTP transfer's `TransferReport.ResponseCode` is the last reply code the server sent, as curl 8.21.0's `%{response_code}` is, on success and on failure.

## Context

- Found in BL-317 (2026-09-27): `TransferRetrier` retries a failed ftp:// or ftps:// transfer whose `Report.ResponseCode` is 4xx (`: FTP error`), but the FTP handler sets no report code today, so the retry never fires.
- Measured on curl 8.21.0, 2026-09-27: a server answering `PASS` with `430` gives `curl: (67) Access denied: 430`, and `--retry 2` retries it with `Warning: Problem : FTP error. Retrying in 1 second. 2 retries left.`; a `421` greeting is exit 28 (retried as a timeout).
- Measure `curl -w "%{response_code}"` against a loopback FTP server for a success and for a 4xx and 5xx failure before pinning.

## Acceptance criteria

- [ ] `%{response_code}` for a successful download and for a `430` login failure is measured on curl 8.21.0 and the handler's `TransferReport.ResponseCode` matches both in unit tests.
- [ ] `dotnet build` is clean with warnings as errors; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

- 2026-09-27 (lane 1): there is no FTP handler to set the code on. `Curl.Protocol.Ftp.UnitLibrary` holds only its `.csproj` and `CLAUDE.md`, and `Curl.Console/ForwardedFtpProtocolHandler.cs` fails every non-proxied `ftp://` transfer with exit 1. Building the handler is a whole protocol, not this task, so it is filed as BL-431 and this task waits on it.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Waits on BL-431: no FTP handler exists yet to report a reply code from
