---
id: BL-5404
title: Close an FTP short download at 150 without reading the final reply, as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5404 — Close an FTP short download at 150 without reading the final reply, as curl does

## Goal

An FTP download that ends with fewer bytes than the server announced closes the control connection at the `150` reply without reading the final `226`/`426`, so `%{response_code}` is `150`, exit is 18 and `-v` shows `* closing connection #0`, as in curl.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-112 (Low: the exit code matches, the write-out value, `-v` text and control traffic differ; Confirmed twice for each server variant). Area: ftp:// partial transfer, Windows.

**Repro.** An FTP server on 127.0.0.1:46030 that answers `SIZE` with `213 10` and `RETR` with `150`, sends 5 bytes on the data connection, closes it, then sends (a) `226 done` or (b) `426 aborted`.
`curl -sS -o o.txt -w "%{response_code}\n" ftp://short:p@127.0.0.1:46030/f.txt` (and again with `-v`)
**Expected (REF).** `150` in both cases, exit 18, `curl: (18) transfer closed with 5 bytes remaining to read`; `-v` shows `* closing connection #0` and no final reply.
**Actual (PORT).** `226` / `426`, exit 18, the same error line; `-v` shows `< 226 done` and no `* closing connection #0`.
**Where to start.** `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs`: `EndRangeAsync`, `DescribeTransferNotOk` and `lastReplyCode` in `RunAsync`. Check the size mismatch before reading the final reply and close without reading it, as curl's `ftp_done` does.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Ftp.UnitTests` (a new file) prove, against a scripted control and data connection, that for the `226` variant the report's response code is 150, the result is exit 18 with `transfer closed with 5 bytes remaining to read`, and the verbose events contain `closing connection #0` and no `226` line.
- [ ] The same test for the `426` variant proves response code 150, exit 18 and no `426` line in the verbose events.
- [ ] A test proves a download whose data length equals the announced size still reads the `226` and reports it as before (no regression).
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: both server variants print `150`, exit 18, and `-v` shows `* closing connection #0` with no `< 226`/`< 426` line. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: both variants print `150`, exit 18, and `-v` shows `* closing connection #0` with no `< 226`/`< 426` line.

## Log

- 2026-11-17: Created.
