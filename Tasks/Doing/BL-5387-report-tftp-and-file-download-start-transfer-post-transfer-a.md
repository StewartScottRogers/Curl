---
id: BL-5387
title: Report TFTP and FILE download start-transfer, post-transfer and connect times as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs, Curl.Original.Protocol.Tftp.UnitTests, Curl.Original.Protocol.File.UnitLibrary, Curl.Original.Protocol.File.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5387 — Report TFTP and FILE download start-transfer, post-transfer and connect times as curl does

## Goal

`%{time_connect}`, `%{time_starttransfer}` and `%{time_posttransfer}` for TFTP and FILE downloads match curl: connect 0, start-transfer before total, post-transfer greater than 0.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-077 (Low, Confirmed by two reviewers, 2 runs each, with and without `--tftp-blksize 1024`: wrong `-w`/`%{json}` values; the file is correct). Area: tftp:// and file:// downloads, write-out, Windows.

Still-open older finding. What changed: the TFTP size and speed are now fixed; the timings remain. Uploads are correct.

**Repro.** A TFTP server on 127.0.0.1:41069 serving `big.bin` (300000 bytes), and the same file on disk.
- `curl -s -o got.bin -w '%{json}' tftp://127.0.0.1:41069/big.bin`
- `curl -s -o got.bin -w '%{json}' file:///C:/path/big.bin` (tests use a drive-less path)

**Expected (REF).** TFTP: `time_connect` 0, `time_starttransfer` (~0.002) < `time_total` (~0.022), `time_posttransfer` about `time_total`. FILE: `time_connect` 0, `time_starttransfer` < `time_total`, `time_posttransfer` > 0.
**Actual (PORT).** TFTP: `time_connect` = `time_namelookup` = `time_pretransfer` (~0.0018), `time_starttransfer` = `time_total`, `time_posttransfer` 0. FILE: `time_connect` 0.000001, `time_starttransfer` = `time_total`, `time_posttransfer` 0.
**Where to start.** `Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs` `Stamp()`, and `WithTimings` in `Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs` (~line 206; for a download it uses `completed` as the first-byte time and `resolved` as the connect time). Start-transfer is stamped at the end, post-transfer is never set, connect is stamped for connectionless schemes.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Tftp.UnitTests` (a new file) prove a TFTP download's report has `time_connect` 0, `time_starttransfer` < `time_total` and `time_posttransfer` > 0 (inject `TimeProvider`, no real clock).
- [ ] New tests in `Curl.Original.Protocol.File.UnitTests` (a new file) prove the same for a FILE download.
- [ ] Tests prove TFTP and FILE uploads keep their current (correct) timings.
- [ ] Tests are platform-neutral: use a drive-less `file:///dir/x` style URL or the handler's injected file system, never `file:///C:/...` (CLAUDE.md).
- [ ] Hand check recorded in the Log: both commands report `time_starttransfer` < `time_total`, `time_posttransfer` > 0 and `time_connect` 0.000000. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: both commands report `time_starttransfer` < `time_total`, `time_posttransfer` > 0 and `time_connect` 0.000000.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 189f7e59 1f484b78 992872fd 374f82e6 182da9f6 a0b49a29.
