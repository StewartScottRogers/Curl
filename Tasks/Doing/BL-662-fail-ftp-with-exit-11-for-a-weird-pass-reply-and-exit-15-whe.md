---
id: BL-662
title: Fail FTP with exit 11 for a weird PASS reply and exit 15 when the PASV host cannot be resolved
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-662 — Fail FTP with exit 11 for a weird PASS reply and exit 15 when the PASV host cannot be resolved

## Goal

The FTP handler ends with exit 11 (`CURLE_FTP_WEIRD_PASS_REPLY`) and curl 8.21.0's message when `PASS` gets a reply curl calls weird, and with exit 15 (`CURLE_FTP_CANT_GET_HOST`) and curl's message when the data-connection host cannot be resolved or connected, where today neither exit is produced.

## Context

- Conformance audit 2026-09-28, row 45 (Minor).
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`; `CurlExitCode.FtpWeirdPassReply` and `FtpCantGetHost` exist in Abstractions. Which PASS replies are "weird" (a `332` without `--ftp-account`? a `2xx` other than `230`? a `4xx`?) and what turns into 15 (with `--ftp-skip-pasv-ip` off and a `227` naming an unreachable address, or an `EPSV` reply on a host that fails) must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp -FtpReply`: `PASS` answered `202`, `332`, `421` and `530`; `PASV` answered with an unroutable address with and without `--ftp-skip-pasv-ip`; stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin each measured exit code and message.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
