---
id: BL-514
title: Report the FTP entry path in %{ftp_entry_path}
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-514 — Report the FTP entry path in %{ftp_entry_path}

## Goal

`%{ftp_entry_path}` prints the directory the FTP server's `PWD` reply named at login, as curl 8.21.0 does, instead of always printing nothing.

## Context

- Conformance audit 2026-09-28, row 41 (Major; sequenced into the opening queue at Stewart's request).
- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps `ftp_entry_path` to `WriteOutValue.FromText(null)`. The FTP session (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`) sends `PWD` and reads the `257` reply; the path needs a home on `TransferReport` (`Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs`).
- `257` quoting (`"/a ""b"""` doubles quotes) and a reply curl cannot parse must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp` (PWD reply varied): `257 "/"`, `257 "/home/u"`, `257 "/a ""b"""`, and a `257` without quotes, each with `-w '%{ftp_entry_path}'` and `-w '%{json}'`; stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin the path the handler reports for each measured reply; `Curl.Output.UnitTests` pin the variable from the report.
- [ ] Non-FTP transfers still print nothing for it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
