---
id: BL-3962
title: CGR-107b: -w timings (time_connect, time_pretransfer, time_starttransfer, time_total) and upload size_request for FTP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs, Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr107TimingTests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerConnectOnlyTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3962 — CGR-107b: -w timings (time_connect, time_pretransfer, time_starttransfer, time_total) and upload size_request for FTP

## Goal

FTP transfers fill `%{time_connect}`, `%{time_pretransfer}`, `%{time_starttransfer}` and `%{time_total}` with real elapsed times, and an FTP upload's `%{size_request}`, as curl 8.22.0 does.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. FTP-8 and FTP-9: Curl prints `0.000000` for every timing (even after a 60 s wait) and `8 0 0 0` for `-T upload.txt -w "%{size_upload} %{size_download} %{size_request} %{size_header}"` where curl prints `8 0 8 0`. The FTP handler puts no timings on its `TransferReport`; see how `HttpProtocolHandler` fills them. If the change belongs in another FTP file, add it to `touches` by the lane rules.

## Acceptance criteria

- [x] An FTP download prints non-zero, ordered times (connect <= pretransfer <= starttransfer <= total) and an 8-byte upload prints `8 0 8 0`, as real curl.
- [x] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [x] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

- Measured on Windows with curl 8.22.0 against `Record-CurlExchange.ps1 -Ftp` (two runs, so the values differ by timing only):
  - Download, `-w "%{time_connect} %{time_pretransfer} %{time_starttransfer} %{time_total}"`: real curl `0.000553 0.058142 0.069681 0.070643`, Curl `0.039712 0.065980 0.092206 0.102414`. Before the fix Curl printed all zeros. Both are non-zero and in order.
  - 8-byte `-T` upload, `-w "%{size_upload} %{size_download} %{size_request} %{size_header} %{time_connect} %{time_pretransfer} %{time_starttransfer}"`: real curl `8 0 8 0 0.062422 0.073874 0.073890`, Curl `8 0 8 0 0.063597 2.097955 2.097955`. Sizes match, and as in curl, starttransfer for an upload is at the end. Curl's pretransfer is larger on that one run because that run took longer, not because the timing is placed differently.
- How the times are set (the sensible default, following curl's FTP timers): pretransfer is when the connect phase ends (greeting, login, `PWD`, or taking up a kept connection). Starttransfer is a download's first data byte, or the end of the transfer for an upload or an empty download. Neither is set when the connect phase fails. Connect and namelookup come from the control connection's `ConnectTimings`. `size_request` is the number of bytes uploaded, as curl counts them for FTP.
- Touches gained `FtpSession.cs`, where the FTP report and the data copy are built, and `FtpProtocolHandlerConnectOnlyTests.cs`, whose expected report now carries timings. No other task in Doing names either file.
- The timings and sizes do not depend on the platform: the tests use a ticking fake `TimeProvider` and do no I/O. The Linux and macOS criterion is met by keeping the change platform-neutral with the fast tests green here.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` first flagged `CopyDataAsync` at complexity 12. Moving the write and progress steps into `WriteReceivedAsync` fixed it: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 2e1a7764 d9aa5d84 40bddbd7 bc4b41f1.
- 2026-10-28: Doing -> Done. FTP reports timings and upload size_request; build clean, fast tests green, quality gates pass
