---
id: BL-943
title: Write curl's Entry path and same-path -v lines for FTP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-931]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-943 — Write curl's Entry path and same-path -v lines for FTP

## Goal

For `ftp://` and `ftps://`, `-v` shows curl 8.21.0's `* Entry path is '<dir>'` after the `257` reply to `PWD`, and `* Request has same path as previous transfer` when the URL path needs no `CWD`.

## Context

- Found in BL-931 (see its Notes for the recordings): curl 8.21.0 (mingw, Schannel) prints `* Entry path is '/'` straight after `< 257 "/" is current directory`, and `* Request has same path as previous transfer` before the first command after it when no `CWD` is sent: a file at the root, a root listing, and every `--ftp-method nocwd` path. With `CWD dir` or `CWD a/b` it is not printed.
- curl's `ftp_parse_url` prints the second line; for `nocwd` with a path that starts with `//` it sets `cwddone` without printing. Measure that case, an OS/400 second `PWD` (BL-782), and `-Q` commands after login before pinning the order.
- Where: `FtpSession.ReadEntryPathAsync` and `TransferPathAsync`, texts in `FtpTransferMessages`. Existing tests that pin `events.Info` as a whole will need their expectations filtered as BL-931 did.

## Acceptance criteria

- [ ] Measured `-v` output for a root file, `/dir/file`, `nocwd`, `singlecwd`, a `//` nocwd path and a `-Q` login quote copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin both lines in order among the command and reply lines through `RecordingTransferEvents.Transcript`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
