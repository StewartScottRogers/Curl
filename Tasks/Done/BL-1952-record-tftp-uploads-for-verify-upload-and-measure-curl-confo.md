---
id: BL-1952
title: Record TFTP uploads for verify upload and measure Curl.Conformance coverage after the tftpd stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1952 — Record TFTP uploads for verify upload and measure Curl.Conformance coverage after the tftpd stand-in

## Goal

The tftpd stand-in records a TFTP write request's DATA blocks for `<verify><upload>`, so test285, test286 and test1243 are measured, and Curl.Conformance.UnitLibrary's coverage is measured after BL-1901.

## Context

BL-1901 added `TftpServerConnector` / `TftpServerChannel` (Curl.Conformance.UnitLibrary) for read requests; a write request gets ERROR 4. Upstream's tests/server/tftpd.c (curl-8_21_0, from the release tarball into a scratch folder) answers WRQ with ACK 0, ACKs each DATA block and writes the file it receives to the log for `<verify><upload>`. Screening (`UpstreamCaseScreening`) records `<verify><upload>` only for smtp, imap, smtps and imaps; add tftp once the stand-in records it, and give `UpstreamCaseRun.UploadedBytes` the TFTP upload. BL-1901 could not run `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` within its cost cap, so the new stand-in's coverage is unmeasured.

## Acceptance criteria

- [x] A WRQ is answered with ACK 0 and each DATA block with its ACK; the received bytes are compared with `<verify><upload>`.
- [x] test285, test286 and test1243 run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary -ReportPath <file>` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30; gaps are closed with tests.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md says the stand-in records uploads.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- The tftpd stand-in now records each new DATA block's bytes, as received, in `TftpServerConnector.UploadedBytes`; a duplicate or out-of-order block is acknowledged (or ignored) as before but not recorded. The runner adds them to `UpstreamCaseRun.UploadedBytes` and screening records `<verify><upload>` for `tftp`.
- Choice: a netascii upload is recorded as received, without tftpd's CR LF to LF reversal, since curl always sends octet (every vendored WRQ case is `mode = octet`).
- test285, test286 and test1243 now run through UpstreamCaseRunner and fail on the known Curl difference already listed for the read cases: Curl sends `test285.txt` where curl sends `/test285.txt` for `tftp://host:port//`. Not on the passing list yet.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary: 100% line, 100% branch, 816 members, 0 failing, worst CRAP 10.
- Fast tests green (Curl.Conformance.UnitTests: 2089 passed, 1106 skipped, 0 failed); dotnet build 0 warnings.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. The tftpd stand-in records TFTP uploads for <verify><upload>; test285, test286 and test1243 run; Curl.Conformance at 100% line and branch
