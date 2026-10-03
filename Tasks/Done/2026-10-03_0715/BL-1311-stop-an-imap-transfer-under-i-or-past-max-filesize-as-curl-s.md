---
id: BL-1311
title: Stop an IMAP transfer under -I or past --max-filesize as curl's download writer does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: FR-084
created: 2026-10-02
completed: 2026-10-03
---
# BL-1311 — Stop an IMAP transfer under -I or past --max-filesize as curl's download writer does

## Goal

An `imap://` transfer honours `ITransferContext.NoBody` (`-I`) and `ITransferContext.MaxFileSize` (`--max-filesize`) as curl 8.21.0 does: under `-I` the first body bytes (a FETCH literal, or a LIST or SEARCH response line) end the transfer with exit 8 and write nothing; past the limit the allowed bytes are written and the transfer ends with exit 63.

## Context

- Today `Curl.Protocol.Imap.UnitLibrary/ImapSession.cs` writes FETCH literals and the untagged LIST/SEARCH/custom-command lines to `context.Output` through `WriteOutputAsync` (line 711) and reports them as received data (lines 557, 669); nothing reads `context.NoBody` or `context.MaxFileSize`.
- curl 8.21.0 writes all of these with `Curl_client_write(data, CLIENTWRITE_BODY, ...)` (`lib/imap.c`, e.g. `imap_state_listsearch_resp` and `imap_state_fetch_resp` lines 1396-1488), so each meets `cw_download_write` in `lib/sendf.c` (tag `curl-8_21_0`):
  - lines 214-224: with `no_body` set and no headers received, the first body bytes close the connection ("ignoring body") and return `CURLE_WEIRD_SERVER_REPLY` (exit 8) without `failf`: no `-v` line, exit text `Weird server reply`;
  - lines 251-291: with `max_filesize` set, a write is cut to the bytes left under the limit, the cut part is written, and when bytes were cut `failf(data, "Exceeded the maximum allowed file size (%ld) with %ld bytes", ...)` returns exit 63. A body exactly at the limit does not fail; 0 is no limit; the count runs across writes.
  - `-I` still sends every command up to the one whose reply carries the body (`imap.c` line 2062 only marks the transfer as info).
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Imap` (default replies and message):
  - `-sv -I imap://u:p@127.0.0.1:PORT/INBOX;UID=1`: exit 8 (`curl: (8) Weird server reply` without `-s`), stdout empty; stderr ends `< * 1 FETCH (UID 1 BODY[] {100}`, `* Found 100 bytes to download`, `{ [100 bytes data]`, `* shutting down connection #0`.
  - `-sv -I imap://u:p@127.0.0.1:PORT/`: exit 8, stdout empty; stderr ends `< * LIST (\HasNoChildren) "/" INBOX`, `{ [35 bytes data]`, `* shutting down connection #0` - the second LIST line is never read.
  - `-sv -I imap://u:p@127.0.0.1:PORT/INBOX?ALL`: exit 8; stderr ends `< * SEARCH 1 2`, `{ [14 bytes data]`, `* shutting down connection #0`.
  - `-sv --max-filesize 3 imap://u:p@127.0.0.1:PORT/INBOX;UID=1`: exit 63, stdout `Fro`; stderr ends `{ [100 bytes data]`, `* Exceeded the maximum allowed file size (3) with 3 bytes`, `* shutting down connection #0`; the transcript ends at `A004 OK FETCH completed` with no LOGOUT.
  Curl today writes the whole body and exits 0 in all four, ending `Connection #0 to host 127.0.0.1:PORT left intact`.
- In the `-I` runs under `-v` the recorder's transcript showed a `LOGOUT` after the stop; re-measure with `Record-CurlExchange.ps1 -Imap` and match what it records rather than assuming either way.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Imap.UnitTests` drive each of the three `-I` cases above through the fake connection with `NoBody = true` and assert exit 8 (`CurlExitCode.WeirdServerReply`), message `Weird server reply`, nothing written to the output, the received-data event of the measured size still reported, no info line for the failure, the connection ending with `shutting down connection #0`, and the commands sent matching the measured transcript.
- [x] A test of the UID FETCH with `MaxFileSize = 3` asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (3) with 3 bytes`, output `Fro`, the message as an info line before `shutting down connection #0`, and no `LOGOUT` sent.
- [x] A test of a LIST with a limit that falls inside the second LIST line asserts the first line written whole and the second cut.
- [x] Tests pin that `MaxFileSize` of 0, `null` and exactly the 100-byte message end with exit 0 and the whole body; an APPEND upload (`-T`) ignores both settings.
- [x] `dotnet build Curl.Protocol.Imap.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Imap.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports no failing member.

## Notes

- Re-measured 2026-10-03 with `Record-CurlExchange.ps1 -Imap` (curl 8.21.0, Schannel):
  the `-I` UID FETCH sends **no** `LOGOUT` (the transcript ends at `A004 UID FETCH`);
  the `-I` LIST and SEARCH send `LOGOUT` after stopping, unseen under `-v`. A
  `--max-filesize 40` LIST writes `* LIST (\HasNoChildren) "/" INBOX\r\n* LIS`, exit 63,
  `LOGOUT` sent. So a listing's refused body logs out and a FETCH's does not.
- Plan (pipeline `feature`, delivered directly - one class and its tests): `ImapSession`
  routes every body write through a new `WriteBodyAsync`, which refuses under `NoBody`
  (exit 8, nothing written) and cuts at `MaxFileSize` (exit 63, the allowed part
  written, the count running across the listing's lines and literal, 0 meaning no
  limit), setting the phase to `Performing` so the handler ends with `shutting down
  connection #0`. `LogoutIfBodyRefusedAsync` sends `LOGOUT` for those two failures on
  the listing path only. `ImapSessionMessages.MaxFileSizeExceeded` formats the message,
  which `-v` writes; `Weird server reply` is already not written.
- Choice: a listed literal (e.g. `-X "FETCH 1 BODY[]"`) cut at the limit follows the
  listing (sends `LOGOUT`), as curl writes it from the same `listsearch` state; not
  measured separately.
- The tests run with no credentials, so `CAPABILITY` is followed directly by the command
  curl sent after its `AUTHENTICATE PLAIN`; the commands are otherwise the measured ones.
- Tests: `ImapProtocolHandlerBodyLimitTests` (10 cases).

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. IMAP stops under -I (exit 8, nothing written) and past --max-filesize (exit 63, allowed bytes written) as curl 8.21.0 does
