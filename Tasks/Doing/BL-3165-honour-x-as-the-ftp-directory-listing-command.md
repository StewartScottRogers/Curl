---
id: BL-3165
title: Honour -X as the FTP directory listing command
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerListingRequestTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3165 — Honour -X as the FTP directory listing command

## Goal

`curl -X <command> ftp://host/` sends the given command as the directory listing command, as upstream does, instead of always sending `LIST`.

## Context

Stewart reported on 2026-10-22: `curl -X NLST ftp://host/` must send `NLST`, and `curl -X 'LIST -a' ftp://host/` must send `LIST -a`; Curl sends plain `LIST`. Upstream's ftp.c uses `--request` (`CURLOPT_CUSTOMREQUEST`) in place of `LIST`/`NLST` for a listing (and, per upstream, not for a file download). Measure curl 8.22.0 with `Record-CurlExchange.ps1` for: a bare `-X NLST`, `-X 'LIST -a'`, `-l` together with `-X`, a listing with a path argument, and `-X` on a file download (which must keep sending RETR). `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs` builds the verb in `ListVerb` / the listing command near lines 1049-1060. The FTP handler currently has no custom-request field; look at how `Curl.Protocol.Pop3.UnitLibrary` and `Curl.Protocol.Imap.UnitLibrary` receive `-X` (see `Curl.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerCustomCommandTests.cs`) and `HttpRequestOptions.CustomMethod` in `Curl.Protocol.Abstractions.UnitLibrary`, and carry the value the same way. If the value must be added to a contract in `Curl.Protocol.Abstractions.UnitLibrary`, that is a shared contract: add the file to this task's `touches` before editing it. Tasks BL-3161 and the FTP size task also edit `FtpSession.cs`; the board serialises them by `touches`.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerListingRequestTests.cs` with a fake FTP server that logs command lines shows `-X NLST` sends exactly `NLST`, and `-X 'LIST -a'` sends exactly `LIST -a`, after the usual `TYPE A` and PASV/EPSV.
- [ ] The NLST case returns the server's bare names on the output, unchanged.
- [ ] `-X` on a file download still sends `RETR`, and no `-X` still sends `LIST` (and `NLST` under `-l`), matching curl 8.22.0's measured behaviour, which the test names.
- [ ] `--ai-help` and `--help` text for `-X` already match (or the task updates the `--ai-help` entry to say FTP listing is covered and a test checks it).
- [ ] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Protocol.Ftp.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 73ac7c58 f42410c0 57934567 e017a1c1 4be569d0.
