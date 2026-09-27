---
id: BL-431
title: Implement FTP download with login, passive mode and RETR in FtpProtocolHandler
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
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

- [x] `FtpProtocolHandler` serves `ftp`, and unit tests driven by a recorded control and data byte stream pin the command sequence curl 8.21.0 sends for `curl ftp://127.0.0.1:<port>/file.txt` and the body written to the output.
- [x] A `430` reply to `PASS` ends the transfer with exit 67 (`LoginDenied`) and `Access denied: 430`, pinned in a unit test.
- [x] `dotnet build` is clean with warnings as errors; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

- Plan: `FtpProtocolHandler` connects the control connection and hands it to
  `FtpDownloadSession`, which holds the conversation; `FtpControlChannel` reads replies a
  line at a time, `FtpUrlPath` splits the path into `CWD`s and a file name, and
  `FtpPassiveReply` reads the `229`/`227` port. Decisions in ADR-0093 (decided by Claude
  under Stewart's delegation).
- Measured first: `Record-CurlExchange.ps1` gained `-Ftp` (a scripted control server with a
  passive data listener, `-FtpReply 'VERB=reply'`, `-FtpData`, `CLOSE`, `RETRDONE`, and a
  `transcript.txt`). About 45 cases recorded against curl 8.21.0 on 2026-09-27; every
  sequence and message in `FtpProtocolHandlerTests` (80 tests) comes from them.
- `touches` widened (rule 3; no task in Doing names either): `Record-CurlExchange.ps1`, to
  measure FTP, and ADR-0093, to record the decisions.
- Beyond the goal, because curl does them and each was measured: directory listing (`TYPE A`,
  `LIST`) for a path ending in `/`, `CWD` per directory, a `230` greeting skipping the
  login, `421` as exit 28, and the 65535-byte reply-line cap (exit 100).
- Review findings acted on: CRLF in `-u` credentials is sent verbatim because curl 8.21.0
  does (measured, ADR-0093); a negative `SIZE` is ignored; the recorder no longer waits
  forever for a data connection. Not acted on: UTF-8 credentials (the Windows build
  sends Latin-1/ANSI bytes, measured); one test class per internal type (the internals are
  reached through the handler and the project has no `InternalsVisibleTo`).
- Not measured, nearest rule followed: a failed data connect returns the connector's
  result without `QUIT`; a failed data read is exit 56.
- The handler is not registered in `Curl.Console` and still ignores `-r`, `-C`, `-T` and
  `-I`; follow-ups filed: BL-432 (-r, -C, -I), BL-433 (-T upload), BL-434 (register in
  Curl.Console), BL-435 and BL-436 (FTP control options), BL-437 (active mode, ftps).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. FtpProtocolHandler downloads and lists ftp:// in passive mode with curl 8.21.0's measured commands and exit codes (430 -> 67 Access denied: 430)
