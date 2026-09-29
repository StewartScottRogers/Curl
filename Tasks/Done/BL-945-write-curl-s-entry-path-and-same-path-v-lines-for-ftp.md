---
id: BL-945
title: Write curl's Entry path and same-path -v lines for FTP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-931]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-945 — Write curl's Entry path and same-path -v lines for FTP

## Goal

For `ftp://` and `ftps://`, `-v` shows curl 8.21.0's `* Entry path is '<dir>'` after the `257` reply to `PWD`, and `* Request has same path as previous transfer` when the URL path needs no `CWD`.

## Context

- Found in BL-931 (see its Notes for the recordings): curl 8.21.0 (mingw, Schannel) prints `* Entry path is '/'` straight after `< 257 "/" is current directory`, and `* Request has same path as previous transfer` before the first command after it when no `CWD` is sent: a file at the root, a root listing, and every `--ftp-method nocwd` path. With `CWD dir` or `CWD a/b` it is not printed.
- curl's `ftp_parse_url` prints the second line; for `nocwd` with a path that starts with `//` it sets `cwddone` without printing. Measure that case, an OS/400 second `PWD` (BL-782), and `-Q` commands after login before pinning the order.
- Where: `FtpSession.ReadEntryPathAsync` and `TransferPathAsync`, texts in `FtpTransferMessages`. Existing tests that pin `events.Info` as a whole will need their expectations filtered as BL-931 did.

## Acceptance criteria

- [x] Measured `-v` output for a root file, `/dir/file`, `nocwd`, `singlecwd`, a `//` nocwd path and a `-Q` login quote copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin both lines in order among the command and reply lines through `RecordingTransferEvents.Transcript`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-29 with curl 8.21.0 (x86_64-w64-mingw32) Schannel through
`Record-CurlExchange.ps1 -Ftp -FtpData 'hello ftp\r\n' -CurlArgs -v,...`, the lines from
`> PWD` on (data-connection lines left out):

- Root file `/file.txt`, root listing `/`, `-T - /up.txt`: `< 257 "/" is current directory`,
  `* Entry path is '/'`, `* Request has same path as previous transfer`, `> EPSV`.
- `/dir/file.txt`: `* Entry path is '/'`, `> CWD dir` - no same-path line.
- `--ftp-method nocwd /a/b/file.txt`, and nocwd listings `/dir/` and `/`: entry path, same-path
  line, `> EPSV` (`LIST dir` for the listing).
- `--ftp-method singlecwd /a/b/file.txt`: entry path, `> CWD a/b`. Multicwd `//file.txt`: `> CWD /`.
- nocwd `//a/file.txt` and `//file.txt`: entry path, `> EPSV`, `SIZE /a/file.txt` - no same-path line
  (curl's `cwddone` without the `infof`).
- `-Q NOOP /file.txt`: entry path, same-path line, `> NOOP`. `-Q NOOP /dir/file.txt`: entry path, `> NOOP`, `CWD dir`.
- Relative `257 "home"`: `> SYST`, `* Entry path is 'home'`, `< 502`, same-path line - the entry line
  comes after SYST is sent, before its reply.
- OS/400 (`257 "QSYS.LIB"`, `215 OS/400 ...`, `SITE 250`, `257 "/QSYS.LIB"`): `> SYST`,
  `* Entry path is 'QSYS.LIB'`, `< 215`, `SITE NAMEFMT 1`, `< 250`, `> PWD`, `< 257 "/QSYS.LIB"`,
  `* Entry path is '/QSYS.LIB'`, same-path line, `> EPSV`.
- `257 slash is current` and `257 ""`: `* Failed to figure out path`, then the same-path line.
  `500 no` to PWD: no entry line, then the same-path line.

Decisions (follow the measurements, no ADR needed):
- The same-path line is reported where the transfer starts after login (before any `-Q` quote),
  from `FtpUrlPath.IsInEntryDirectory`: no `CWD` argument, and under nocwd a decoded path that does
  not start with `/`. An upload reports it after the missing-filename check, as curl's
  `ftp_parse_url` does.
- `Failed to figure out path` was measured beside the entry line and lives in the same place, so it is
  written too (`FtpTransferMessages.EntryPathReply`).
- Existing tests that pinned `events.Info` whole now compare `RecordingTransferEvents.InfoPastTheEntryPath`
  (ActiveMode and TimeCondition tests); the data-connection transcript tests pin the new lines and
  their shifted offsets. New tests: `FtpProtocolHandlerEntryPathEventTests`.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -v shows curl 8.21.0's Entry path, Failed to figure out path and Request has same path lines for FTP
