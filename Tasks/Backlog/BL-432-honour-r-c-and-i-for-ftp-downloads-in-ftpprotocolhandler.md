---
id: BL-432
title: Honour -r, -C and -I for ftp:// downloads in FtpProtocolHandler
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-431]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-27
completed:
---
# BL-432 — Honour -r, -C and -I for ftp:// downloads in FtpProtocolHandler

## Goal

`FtpProtocolHandler` honours `ITransferContext.Range` (`-r`), `ResumeFrom` (`-C <offset>` and `-C -`) and `NoBody` (`-I`) on an `ftp://` download, sending curl 8.21.0's commands and writing curl's output and exit code for each, instead of downloading the whole file.

## Context

- BL-431 added `FtpProtocolHandler` (`Curl.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs`, with `FtpDownloadSession.cs` and `FtpControlChannel.cs`); its conversation is recorded in ADR-0093 (`Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md`). ADR-0093's Consequences say the handler ignores `Range`, `ResumeFrom`, `Upload` and `NoBody` and "must be fixed before it is registered"; BL-434 (registering it in `Curl.Console`) waits on this task.
- The context properties already exist in `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`: `ByteRange? Range`, `long? ResumeFrom`, `bool NoBody`. No abstraction change is needed.
- Measure before pinning. `Record-CurlExchange.ps1 -Ftp` runs a scripted control server: `-FtpReply 'VERB=reply'` overrides one command's reply (`CLOSE` hangs up instead of answering; `RETRDONE` sets the reply after the data), and `-FtpData` sets the served file. The server answers an unknown command `502` today, so `REST` and `MDTM` need an override (e.g. `-FtpReply 'REST=350 Restarting'`) or a default added to the recorder; extend the recorder rather than writing a server (root `CLAUDE.md`, "No Python"). It always serves the whole `-FtpData` on `RETR`; if curl's behaviour for a range depends on the server honouring `REST` (serving from the offset) or on the data stopping early, extend the recorder so the served bytes start at the last `REST` offset.
- Cases to record with curl 8.21.0 (the reference curl `Record-CurlExchange.ps1` finds): `-r 0-4`, `-r 5-` , `-r -3`, `-C 5`, `-C -` with and without an existing output file, a `REST` refused with `5xx`, a `-C` offset past the `SIZE` count, and `-I` on a file and on a directory URL. Pin the command sequence (`REST`, `ABOR`, `MDTM`, whatever curl sends), stdout bytes, stderr message and exit code for each.
- Where curl's text is `curl: (N) …`, the exit code comes from `CurlExitCode` in `Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs`.
- Upstream reference for the options: https://curl.se/docs/manpage.html (`-r`, `-C`, `-I`) and https://curl.se/libcurl/c/libcurl-errors.html; the measured curl 8.21.0 wins where the two differ.
- Record the measured behaviour, and any case not measured and why, as an addendum to ADR-0093 or a new ADR "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerTests.cs` (or a new `FtpProtocolHandlerRangeTests.cs` beside it) pin, for each of `-r 0-4`, `-r 5-`, `-r -3`, `-C 5`, `-C -` and `-I` on a file, the command bytes the handler sends and the output bytes it writes, both matching curl 8.21.0 as recorded with `Record-CurlExchange.ps1 -Ftp`.
- [ ] A refused `REST` and a `-C` offset past the file's `SIZE` each return the `CurlExitCode` and message curl 8.21.0 printed, pinned in a named test.
- [ ] The measured cases (curl version, date, what was sent and printed) are recorded in an ADR (addendum to ADR-0093 or a new one) marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member of `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

## Log

- 2026-09-27: Created.
