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
completed:
---
# BL-1138 — Fail an IMAP transfer whose -D write fails with exit 23 as curl does

## Goal

An `imap://` transfer whose write of a response line to the `-D` stream fails ends with the exit code, message and `LOGOUT` behaviour real curl 8.21.0 shows (expected exit 23), instead of the `IOException` escaping `ImapSession.RunAsync`.

## Context

- BL-1132 made `ImapControlChannel.ReportLineAsync` write every response line to `DumpHeaderOutput` (`Curl.Protocol.Imap.UnitLibrary/ImapControlChannel.cs`). A failed write throws `IOException` (with `curl: Failed writing headers to <file>` already printed by `Curl.Console/DumpHeaderOutputStream.cs`), and `ImapSession.RunAsync` catches no `IOException`, so it escapes the handler.
- In curl 8.21.0 `Curl_pp_readresp` returns the `Curl_client_write(CLIENTWRITE_INFO)` error, which is `CURLE_WRITE_ERROR`. Measure real curl first with `Record-CurlExchange.ps1 -Imap` and `-D` pointed at an unwritable destination (e.g. a full device or a directory) for the exit code, stderr text, and whether `LOGOUT` is sent.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Imap.UnitTests` with a `DumpHeaderOutput` that throws `IOException` pins the measured exit code, error message and bytes sent.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
