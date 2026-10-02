---
id: BL-1138
title: Fail an IMAP transfer whose -D write fails with exit 23 as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1132]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1138 — Fail an IMAP transfer whose -D write fails with exit 23 as curl does

## Goal

An `imap://` transfer whose write of a response line to the `-D` stream fails ends with the exit code, message and `LOGOUT` behaviour real curl 8.21.0 shows (expected exit 23), instead of the `IOException` escaping `ImapSession.RunAsync`.

## Context

- BL-1132 made `ImapControlChannel.ReportLineAsync` write every response line to `DumpHeaderOutput` (`Curl.Protocol.Imap.UnitLibrary/ImapControlChannel.cs`). A failed write throws `IOException` (with `curl: Failed writing headers to <file>` already printed by `Curl.Console/DumpHeaderOutputStream.cs`), and `ImapSession.RunAsync` catches no `IOException`, so it escapes the handler.
- In curl 8.21.0 `Curl_pp_readresp` returns the `Curl_client_write(CLIENTWRITE_INFO)` error, which is `CURLE_WRITE_ERROR`. Measure real curl first with `Record-CurlExchange.ps1 -Imap` and `-D` pointed at an unwritable destination (e.g. a full device or a directory) for the exit code, stderr text, and whether `LOGOUT` is sent.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Imap.UnitTests` with a `DumpHeaderOutput` that throws `IOException` pins the measured exit code, error message and bytes sent.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports no failing member.

## Notes

- Measured 2026-10-01, curl 8.21.0 (Windows, Schannel), `Record-CurlExchange.ps1 -Imap -Curl <Git bash>` running `curl -sS -D - -o <file> -u u:p imap://127.0.0.1:18143/INBOX;UID=1` with stdout piped into a reader that had exited (`{ sleep 0.5; curl ...; } | true`): exit 23, stderr `curl: Failed writing headers to -` then `curl: (23) client returned ERROR on write of 66 bytes` (the greeting line, CRLF included); nothing sent, no `LOGOUT`.
- Piped into `head -c 66` instead, so the greeting is accepted and the next line refused: exit 23, `client returned ERROR on write of 55 bytes` (the `* CAPABILITY` line); nothing sent after `A001 CAPABILITY`, no `LOGOUT`.
- Done: `ImapControlChannel.ReportLineAsync` turns the `-D` write's `IOException` into the new `ImapHeaderWriteFailedException` (not an `IOException`, so no output-write catch swallows it), and `ImapSession.RunAsync` ends the transfer with exit 23 and `ImapSessionMessages.HeaderWriteFailed(<line length>)`, sending nothing more, the same text as FTP and file:// (BL-050). `-v` writes the message as a `*` line, as curl does for its own messages. The `curl: Failed writing headers to <file>` line stays in `Curl.Console`'s `DumpHeaderOutputStream`.
- Tests: 2 new in `ImapProtocolHandlerDumpHeaderTests`; `FailingOutputStream` gained `WritesBeforeFailure`. 383 IMAP tests pass; quality measure 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. an imap:// transfer whose -D write fails ends with exit 23 and curl's message, sending nothing more
