---
id: BL-1291
title: Cut a POP3 download at --max-filesize with curl's exit 63
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: FR-084
created: 2026-10-02
completed:
---
# BL-1291 — Cut a POP3 download at --max-filesize with curl's exit 63

## Goal

A `pop3://` transfer whose body (a RETR message, a LIST or UIDL listing, a custom `-X` command's multi-line reply) runs past `ITransferContext.MaxFileSize` writes the bytes under the limit and ends with exit 63, `Exceeded the maximum allowed file size (N) with N bytes`, still sending `QUIT`, as curl 8.21.0 does.

## Context

- Today `Curl.Protocol.Pop3.UnitLibrary/Pop3Session.cs` `WritePiecesAsync` reports each body piece as received data and writes it to `context.Output`; nothing reads `context.MaxFileSize`. (`-I` already matches curl: measured identical on 2026-10-02.)
- curl 8.21.0 writes the POP3 body with `Curl_client_write(data, CLIENTWRITE_BODY, ...)` (`lib/pop3.c` `pop3_write`), so each piece meets `cw_download_write` in `lib/sendf.c` lines 251-291 (tag `curl-8_21_0`): a write is cut to the bytes left under the limit, the cut part is written, and when bytes were cut `failf(data, "Exceeded the maximum allowed file size (%ld) with %ld bytes", max_filesize, bytecount)` returns `CURLE_FILESIZE_EXCEEDED` (exit 63). A body exactly at the limit does not fail; 0 is no limit; the count runs across pieces.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Pop3` (default message) and `-sv --max-filesize 3 pop3://u:p@127.0.0.1:PORT/1`: exit 63, stdout `Fro`; stderr ends `> RETR 1`, `< +OK 133 octets`, `{ [24 bytes data]`, `* Exceeded the maximum allowed file size (3) with 3 bytes`, `* shutting down connection #0`; the transcript shows the rest of the message arriving and then `> QUIT` / `< +OK Bye` (the QUIT is not echoed to stderr, as `QuitAsync`'s `StopReporting` already arranges). Curl today writes the whole message and exits 0.
- The received-data event carries the whole piece (`{ [24 bytes data]` for the first 24-byte line) even when only part of it is written.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Pop3.UnitTests` drives the measured RETR with `MaxFileSize = 3` and asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `Fro`, one received-data event of 24 bytes, the message as an info line followed by `shutting down connection #0`, and `QUIT` sent before the session ends.
- [ ] A test with a limit that falls inside the message's third line asserts the first two lines written whole and the third cut, with both numbers in the message equal to the limit.
- [ ] A test of a LIST (`pop3://u:p@host/`) with a limit inside the listing pins the same cut for a listing.
- [ ] Tests pin that `MaxFileSize` of 0, `null` and exactly the message's length end with exit 0 and the whole message.
- [ ] `dotnet build Curl.Protocol.Pop3.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Pop3.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-02: Created.
