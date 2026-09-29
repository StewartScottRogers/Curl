---
id: BL-637
title: Honour -z and -R for FTP downloads through MDTM
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-637 — Honour -z and -R for FTP downloads through MDTM

## Goal

An FTP download with `-z <date>` sends `MDTM` and skips the transfer when the condition is unmet (a success with no body, as `TransferResult.TimeConditionNotMet` does for `file://`), and with `-R` reports the `MDTM` time on `TransferResult.SourceLastWriteTimeUtc` so `Curl.Console` stamps the output file, both as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 26 (Major): FTP ignores `-z` and `-R`.
- `ITransferContext.TimeCondition` and `TransferResult.SourceLastWriteTimeUtc` already exist (FR-009, FR-011 for `file://`); `Curl.Console` already applies `-R` through `IFileTimeSetter`. Only the FTP handler changes. Dates parse with `CurlDateParser` (ADR-0074) where needed; `MDTM` replies are `213 YYYYMMDDHHMMSS`.
- `Record-CurlExchange.ps1 -Ftp` answers `MDTM 213 20260927123456` by default.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp`: `-z` older and newer than the MDTM time, `-z -<date>`, `-R -o out`, and `MDTM` answered `550`; commands, stdout, stderr, exit code and the output file's time copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin commands and results (including `TimeConditionNotMet` and the reported time) for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Touches widened: `Curl.Protocol.Abstractions.UnitLibrary` and `.UnitTests`, because the handler cannot see `-R` without a new `ITransferContext.RemoteTime` (curl sends `MDTM` only when asked, so it cannot be sent always), and ADR-0093 for its addendum. No task in Doing named them. `Curl.Console` also needs one line (`RemoteTime = options.RemoteTime`), but BL-732 holds it, so that wiring is filed as BL-854 rather than widening this task.

Measured 2026-09-29, curl 8.21.0 (mingw64, Schannel), `Record-CurlExchange.ps1 -Ftp -FtpData hello` against `ftp://127.0.0.1:52137/dir/f.txt`, `MDTM` answered `213 20260927123456` unless stated. Every run exited 0 unless stated; stderr below is the relevant `-v` lines.

| Case | Commands after `CWD dir` | stdout | stderr / output file |
| --- | --- | --- | --- |
| plain | `EPSV TYPE I SIZE RETR QUIT` (no `MDTM`) | hello | |
| `-z 20200101` | `MDTM EPSV TYPE I SIZE RETR QUIT` | hello | |
| `-z 20300101` | `MDTM QUIT` | (none) | `The requested document is not new enough` |
| `-z 'Sun, 27 Sep 2026 12:34:56 GMT'` (equal) | `MDTM QUIT` | (none) | not new enough |
| `-z -20300101` | `MDTM ... RETR QUIT` | hello | |
| `-z -20200101` | `MDTM QUIT` | (none) | `The requested document is not old enough` |
| `-z '-Sun, 27 Sep 2026 12:34:56 GMT'` (equal) | `MDTM ... RETR QUIT` | hello | |
| `-R -o out` | `MDTM ... RETR QUIT` | | out LastWriteTimeUtc `2026-09-27T12:34:56Z` |
| `-R -z 20300101 -o out` | `MDTM QUIT` | | out not created |
| `-z 20200101`, `MDTM=550` | `MDTM ... RETR QUIT` | hello | `MDTM failed: file does not exist or permission problem, continuing`, `Skipping time comparison` |
| `-R -o out`, `MDTM=550` | `MDTM ... RETR QUIT` | | MDTM failed ...; out keeps its write time |
| `-z 20300101`, `MDTM=500` | transfers | hello | `unsupported MDTM reply format`, `Skipping time comparison` |
| `-z`, `213 garbage` / `213 2026092712345` / `213 20261327123456` / `213 19700101000000` | transfers | hello | `Skipping time comparison` |
| `-z 19700101` | transfers | hello | `Skipping time comparison` |
| `-z '...12:34:55 GMT'`, `213 20260927123456.789` | transfers | hello | |
| `-R -o out`, `213 20260927123456.789` | transfers | | out `2026-09-27T12:34:56Z` |
| `-z 20300101 .../dir/` | `EPSV TYPE A LIST QUIT` (no `MDTM`) | listing | |
| `-l -z 20300101` (file URL) | `MDTM QUIT` | | not new enough |
| `-I -z 20300101` | `MDTM QUIT` | `Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT` | not new enough |
| `-I -z 20200101` | `MDTM TYPE I SIZE REST 0 QUIT` | the three head lines | |
| `-I -z 20300101`, `MDTM=550` | `MDTM TYPE I SIZE REST 0 QUIT` | Content-Length, Accept-ranges | MDTM failed ..., Skipping ... |
| `-T up.txt -z 20300101` | `MDTM QUIT`, nothing stored | | not new enough |
| `-T up.txt -z 20200101`, or `-R` | `MDTM EPSV TYPE I STOR QUIT` | | |
| `-Q NOOP -Q +NOOP -z 20300101` | `NOOP` before `CWD`, then `MDTM QUIT` (no `+NOOP`) | | |
| `-Q -NOOP -z 20300101` | `MDTM NOOP QUIT`; with `NOOP=500 no`, exit 21 | | |
| `-s -w '%{response_code} %{size_download}' -z 20300101` | | `213 0` | |

Decisions (ADR-0093's BL-637 addendum): the time is reported only under `-R`, so no other result changes; a `213` time before 1970 is unknown, from curl's `filetime > 0` test rather than a measurement. The `-v` lines in the table go through `ITransferEvents.ReportInfo`; `Remembering we are in directory "dir/"` and `Maxdownload = -1` still do not, as before this task.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. FTP -z sends MDTM and skips an unmet transfer; -R reports the MDTM time on SourceLastWriteTimeUtc
