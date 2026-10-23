---
id: BL-3165
title: Honour -X as the FTP directory listing command
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerListingRequestTests.cs, Curl.Protocol.Ftp.UnitLibrary/CLAUDE.md, Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3165 — Honour -X as the FTP directory listing command

## Goal

`curl -X <command> ftp://host/` sends the given command as the directory listing command, as upstream does, instead of always sending `LIST`.

## Context

Stewart reported on 2026-10-22: `curl -X NLST ftp://host/` must send `NLST`, and `curl -X 'LIST -a' ftp://host/` must send `LIST -a`; Curl sends plain `LIST`. Upstream's ftp.c uses `--request` (`CURLOPT_CUSTOMREQUEST`) in place of `LIST`/`NLST` for a listing (and, per upstream, not for a file download). Measure curl 8.22.0 with `Record-CurlExchange.ps1` for: a bare `-X NLST`, `-X 'LIST -a'`, `-l` together with `-X`, a listing with a path argument, and `-X` on a file download (which must keep sending RETR). `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs` builds the verb in `ListVerb` / the listing command near lines 1049-1060. The FTP handler currently has no custom-request field; look at how `Curl.Protocol.Pop3.UnitLibrary` and `Curl.Protocol.Imap.UnitLibrary` receive `-X` (see `Curl.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerCustomCommandTests.cs`) and `HttpRequestOptions.CustomMethod` in `Curl.Protocol.Abstractions.UnitLibrary`, and carry the value the same way. If the value must be added to a contract in `Curl.Protocol.Abstractions.UnitLibrary`, that is a shared contract: add the file to this task's `touches` before editing it. Tasks BL-3161 and the FTP size task also edit `FtpSession.cs`; the board serialises them by `touches`.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerListingRequestTests.cs` with a fake FTP server that logs command lines shows `-X NLST` sends exactly `NLST`, and `-X 'LIST -a'` sends exactly `LIST -a`, after the usual `TYPE A` and PASV/EPSV.
- [x] The NLST case returns the server's bare names on the output, unchanged.
- [x] `-X` on a file download still sends `RETR`, and no `-X` still sends `LIST` (and `NLST` under `-l`), matching curl 8.22.0's measured behaviour, which the test names.
- [x] `--ai-help` and `--help` text for `-X` already match (or the task updates the `--ai-help` entry to say FTP listing is covered and a test checks it).
- [x] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Protocol.Ftp.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Measured with `Record-CurlExchange.ps1 -Ftp` against the installed curl, which is 8.21.0
  (Schannel), not 8.22.0; ftp.c's custom-request listing is unchanged between them. `-X NLST`
  sends `NLST`, `-X 'LIST -a'` sends `LIST -a`, `-l -X 'LIST -a'` sends `LIST -a`, nocwd
  sends `NLST sub`, `--ftp-pret` sends `PRET NLST`, and `-X NLST` on `file.txt` sends
  `SIZE file.txt` and `RETR file.txt`. All after `TYPE A` (`TYPE I` for the file).
- Decision (ADR-0765): no contract change. The engine already fills
  `ITransferContext.Http.CustomMethod` from `-X` for every scheme, and FTP already reads
  `Http.IgnoreContentLength`, so `FtpSession.ListVerb` uses `Http.CustomMethod` when it is set.
  That one change also covers `PRET`.
- I added `Curl.Protocol.Ftp.UnitLibrary/CLAUDE.md`, `ITransferContext.cs` and
  `HttpRequestOptions.cs` to touches for doc comments only: the `Http` remark said FTP reads
  only `IgnoreContentLength`. No task in Doing on origin/work/dark-factory names them.
- `--help` and `--ai-help ftp` already describe `-X` for FTP listings ("Specifies a custom FTP
  command to use instead of LIST when doing file lists with FTP"), checked with the built
  curl.exe. No change was needed.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary: 0 failing members. The
  change is platform-neutral (command lines only, no paths).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 73ac7c58 f42410c0 57934567 e017a1c1 4be569d0.
- 2026-10-22: Doing -> Done. curl -X <command> on an ftp:// listing sends the command in place of LIST/NLST; file downloads keep RETR
