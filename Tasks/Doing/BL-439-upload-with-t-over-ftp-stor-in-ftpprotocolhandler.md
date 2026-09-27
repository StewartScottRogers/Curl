---
id: BL-439
title: Upload with -T over ftp:// (STOR) in FtpProtocolHandler
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-431]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-27
completed:
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

- [ ] Tests in `Curl.Protocol.Ftp.UnitTests` (e.g. `FtpProtocolHandlerUploadTests.cs`) pin, for a plain `-T` upload to a file URL and to a directory URL, the command bytes sent on the control connection and the bytes written to the data connection, matching curl 8.21.0 as recorded with `Record-CurlExchange.ps1 -Ftp`.
- [ ] A refused `STOR`, a refused `CWD` and a non-226 end of transfer each return the `CurlExitCode` and message curl 8.21.0 printed, with `QUIT` sent or not as curl did, each pinned in a named test.
- [ ] `-C 5 -T` and `-C - -T` send curl 8.21.0's measured commands (pinned in named tests), or, if not implemented, return the measured behaviour's nearest rule recorded in the ADR below.
- [ ] `Record-CurlExchange.ps1 -Ftp` records the bytes curl sends on a `STOR` data connection, and its comment-based help documents the new behaviour.
- [ ] The measured cases are recorded in an ADR (addendum to ADR-0093 or a new one) marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member of `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
