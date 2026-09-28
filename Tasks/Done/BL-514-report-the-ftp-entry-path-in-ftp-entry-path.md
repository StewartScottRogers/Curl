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
completed: 2026-09-28
---
# BL-514 — Report the FTP entry path in %{ftp_entry_path}

## Goal

`%{ftp_entry_path}` prints the directory the FTP server's `PWD` reply named at login, as curl 8.21.0 does, instead of always printing nothing.

## Context

- Conformance audit 2026-09-28, row 41 (Major; sequenced into the opening queue at Stewart's request).
- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps `ftp_entry_path` to `WriteOutValue.FromText(null)`. The FTP session (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`) sends `PWD` and reads the `257` reply; the path needs a home on `TransferReport` (`Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs`).
- `257` quoting (`"/a ""b"""` doubles quotes) and a reply curl cannot parse must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp` (PWD reply varied): `257 "/"`, `257 "/home/u"`, `257 "/a ""b"""`, and a `257` without quotes, each with `-w '%{ftp_entry_path}'` and `-w '%{json}'`; stdout, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin the path the handler reports for each measured reply; `Curl.Output.UnitTests` pin the variable from the report.
- [x] Non-FTP transfers still print nothing for it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-28 against curl 8.21.0 (mingw, Schannel):
`Record-CurlExchange.ps1 -Ftp -FtpData hi -FtpReply 'PWD=<reply>' -CurlArgs -sS,-o,NUL,-w,<fmt>,ftp://127.0.0.1:<port>/f.txt`,
`<fmt>` `%{ftp_entry_path}|` and `%{json}` (the `ftp_entry_path` member shown).

| PWD reply | `%{ftp_entry_path}` stdout | `%{json}` member | stderr | exit |
| --- | --- | --- | --- | ---: |
| `257 "/" is cwd` | `/` | `"/"` | empty | 0 |
| `257 "/home/u" is cwd` | `/home/u` | `"/home/u"` | empty | 0 |
| `257 "/a ""b""" is cwd` | `/a "b"` | `"/a \"b\""` | empty | 0 |
| `257 /home/u is cwd` (no quotes) | empty | `null` | empty | 0 |
| `257 ""` | empty | `null` | empty | 0 |
| `257 rubbish "/r" x` | `/r` | `"/r"` | empty | 0 |
| `257 "home" is cwd` | `home` | `"home"` | empty | 0 (curl also sends `SYST` - filed as BL-781) |
| `550 no` | empty | `null` | empty | 0 |
| `257 "/x` (quote never closed) | empty | `null` | `curl: (8) Weird server reply` | 8, no `QUIT` |
| `257 "/x is cwd` | empty | `null` | `curl: (8) Weird server reply` | 8, no `QUIT` |

Delivered: `TransferReport.FtpEntryPath` (Abstractions); `FtpEntryPath.TryRead` (Ftp) reads
from the first `"` after the code and its space to the next lone `"`, undoubling `""`, empty
name meaning none, an unended name exit 8 `Weird server reply` with no `QUIT`;
`FtpSession` puts it on every report after `PWD`; `%{ftp_entry_path}` (Output) prints it.
No design choice beyond matching curl, so no ADR. Output text and JSON rendering are the
existing `WriteOutValue.FromText` path; HTTP/file/failed-transfer tests still pin it empty.
Follow-up filed: BL-781 (`SYST` after a relative directory).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. %{ftp_entry_path} prints the directory the PWD 257 reply quotes, as curl 8.21.0; an unended quote is exit 8
