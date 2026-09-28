---
id: BL-439
title: Upload with -T over ftp:// (STOR) in FtpProtocolHandler
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-431]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-439 — Upload with -T over ftp:// (STOR) in FtpProtocolHandler

## Goal

When `ITransferContext.Upload` is set (`-T`), `FtpProtocolHandler` sends curl 8.21.0's upload conversation for an `ftp://` URL (login, `CWD`s, passive data connection, `TYPE I`, `STOR <file>`), writes the upload stream to the data connection, and returns curl's exit code and message for each failure, instead of downloading.

## Context

- BL-431 added `FtpProtocolHandler` (`Curl.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs`, `FtpDownloadSession.cs`, `FtpControlChannel.cs`); its download conversation is recorded in ADR-0093 (`Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md`). ADR-0093's Consequences say uploads are not implemented and the handler ignores `Upload`, which must be fixed before it is registered; BL-434 (registering it in `Curl.Console`) waits on this task.
- `ITransferContext.Upload` (`Stream?`), `ResumeFrom` and `ResumeUploadFromUnknownOffset` already exist in `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`. `Curl.Console` already builds the upload URL (a `-T file` against a URL ending in `/` gets the file name appended, `Curl.Cli.UnitLibrary/UploadUrl.cs`), so the handler receives the final path.
- The login, `PWD`, `CWD` and `EPSV`/`PASV` steps and their failure rules are ADR-0093's and should be shared with the download path, not copied.
- Measure before pinning with `Record-CurlExchange.ps1 -Ftp`. Today its server answers `STOR` and `APPE` with `502` and only serves data on `RETR`/`LIST`; extend it so `STOR` (and `APPE`, if curl sends it for `-C -`) answers `150`, accepts the passive data connection, records the bytes received on it into the transcript, and answers `226` (or the `RETRDONE`-style override) after the client closes it. Extend the recorder rather than writing a server (root `CLAUDE.md`, "No Python").
- Cases to record with curl 8.21.0: `-T file ftp://host/dir/file`, `-T file ftp://host/dir/` , an empty upload file, `STOR` refused with `553`, `CWD` refused (and whether curl sends `MKD`, which it should not without `--ftp-create-dirs`, BL-436), the end-of-transfer reply not `226`, `-C 5 -T file` and `-C - -T file` (curl's `SIZE`/`APPE` or `REST` use). Pin commands, stdout, stderr and exit code for each.
- Exit codes come from `CurlExitCode` (`Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs`). Upstream reference: https://curl.se/docs/manpage.html (`-T`, `-C`) and https://curl.se/libcurl/c/libcurl-errors.html; the measured curl 8.21.0 wins.
- `-a/--append` is not parsed onto `ITransferContext` today and is out of scope.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Ftp.UnitTests` (e.g. `FtpProtocolHandlerUploadTests.cs`) pin, for a plain `-T` upload to a file URL and to a directory URL, the command bytes sent on the control connection and the bytes written to the data connection, matching curl 8.21.0 as recorded with `Record-CurlExchange.ps1 -Ftp`.
- [x] A refused `STOR`, a refused `CWD` and a non-226 end of transfer each return the `CurlExitCode` and message curl 8.21.0 printed, with `QUIT` sent or not as curl did, each pinned in a named test.
- [x] `-C 5 -T` and `-C - -T` send curl 8.21.0's measured commands (pinned in named tests), or, if not implemented, return the measured behaviour's nearest rule recorded in the ADR below.
- [x] `Record-CurlExchange.ps1 -Ftp` records the bytes curl sends on a `STOR` data connection, and its comment-based help documents the new behaviour.
- [x] The measured cases are recorded in an ADR (addendum to ADR-0093 or a new one) marked "Decided by Claude under Stewart's delegation".
- [x] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member of `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

- `touches` gained ADR-0093's file: the acceptance criteria ask for an ADR addendum, and no
  task in Doing (BL-396, BL-435) names it.
- Measured 15 cases with curl 8.21.0 on 2026-09-27; all are in ADR-0093's BL-439 addendum and
  pinned in `FtpProtocolHandlerUploadTests` (20 test methods, 23 cases). `-C 5 -T` and `-C - -T` are
  implemented as measured (`APPE`, with `SIZE` first for `-C -`), not deferred.
- `FtpDownloadSession` was renamed `FtpSession`: it now holds uploads too, and the login,
  `PWD`, `CWD`, `EPSV`/`PASV` and `TYPE` steps are shared, not copied. The byte counter is
  `bytesTransferred` for both directions; an upload's result counts the bytes sent.
  BL-436's Context still names `FtpDownloadSession.cs` (a Backlog task outside this task's
  `touches`); read it as `FtpSession.cs`.
- `FtpUploadOffset` holds the skip rule: seekable non-empty source moved past the offset,
  `QUIT` with exit 0 when the offset covers it; empty or non-seekable source skips nothing.
- Defaults taken where curl could not be measured (recorded in the addendum): a failed
  upload read ends the upload like end of source (the HTTP handler's rule); a failed data
  write is exit 55 with no `QUIT`; `-T` wins over `-I`; `STOR` replies below 400 let the
  upload go ahead (curl checks `>= 400`).
- Not honoured on FTP uploads yet: `--crlf` (`ConvertLineEndings`) and `-a` (out of scope).
- `Record-CurlExchange.ps1 -Ftp` now answers `STOR`/`APPE` with `150`, records the data
  connection's bytes to `upload.bin` and the transcript, and takes a `STORDONE` override.
- Gates: `dotnet build` clean; fast tests green (17 assemblies, FTP 137); `dotnet format`
  clean; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary`: 100% line, 100%
  branch, 89 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ftp:// -T uploads send curl 8.21.0's STOR/APPE conversation with its exit codes, measured and pinned
