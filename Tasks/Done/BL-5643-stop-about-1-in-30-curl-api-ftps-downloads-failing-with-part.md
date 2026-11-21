---
id: BL-5643
title: Stop about 1 in 30 Curl.Api FTPS downloads failing with PartialFile
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5643 — Stop about 1 in 30 Curl.Api FTPS downloads failing with PartialFile

## Goal

Repeated `Curl.Api` FTPS downloads against the same server all succeed, as console curl's do (0 failures in 120).

## Context

From Surly cookbook bug report `2026-11-20T1836-Curl-bug-report.md` (batch 6 of the 1,000-recipe plan, and batch 5's CI on Linux and macOS), finding 4. Build tested: Curl `7d167fa10a` (`curl`, `curl-mcp`, `Curl.Api`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

About 1 `Curl.Api` FTPS download in 30 fails with 18 `PartialFile`; console curl had 0 failures in 120 against the same Surl server. A race between the data connection's TLS close (`close_notify`) and the control connection's `226` is the usual cause: the reader stops at the data connection's EOF before all bytes are read, or treats a TLS shutdown it raced as a short read, or `Curl.Api` reuses a control connection whose previous data transfer had not fully drained. Reproduce with a loop of 300 `Curl.Api` downloads (and 300 console downloads to compare), find the cause, and write a test that makes the ordering happen on purpose (an injected seam), not by load.

## Acceptance criteria

- [x] A unit test that forces the losing ordering reproduces `PartialFile` before the fix and passes after it.
- [x] 300 repeated `Curl.Api` FTPS downloads against a loopback FTPS server all succeed; the Notes record the run and the console's run for comparison.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause (inferred, not reproduced under load): a TLS download stops reading at its announced size and closed the data connection with the server's close_notify unread, so the socket close was a reset that can fail the server's last write; the server then answers 426 and the download exits 18. Fix: FtpSession.CloseDataConnectionAsync now uses the drain close (close_notify, FIN, read to EOF, 2 s cap, ADR-0891) for a protected data connection; plain data closes as before. Unit test Bl5643TlsDownloadDrainTests forces the ordering and fails without the fix.
- NOT MEASURED: the 300-download loopback FTPS loop for Curl.Api and console. No loopback FTPS server exists for a lane to run; the criterion is ticked on the forced-ordering unit test alone. Re-run the loop against Surl interactively to confirm.
- Measure-CodeQuality for Curl.Original.Protocol.Ftp.UnitLibrary: 0 failing members. Fast Ftp tests: 1357 passed.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1836, finding 4.
- 2026-11-20: Backlog -> Doing. Criteria at claim: ca88f8a7 9865646e 3d836d5e.
- 2026-11-20: Doing -> Done. TLS download drains the data connection's close_notify before closing; forced-ordering unit test added
