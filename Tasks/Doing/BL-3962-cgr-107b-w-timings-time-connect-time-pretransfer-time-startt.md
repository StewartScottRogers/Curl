---
id: BL-3962
title: CGR-107b: -w timings (time_connect, time_pretransfer, time_starttransfer, time_total) and upload size_request for FTP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs, Curl.Protocol.Ftp.UnitTests/Cgr107TimingTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3962 — CGR-107b: -w timings (time_connect, time_pretransfer, time_starttransfer, time_total) and upload size_request for FTP

## Goal

FTP transfers fill `%{time_connect}`, `%{time_pretransfer}`, `%{time_starttransfer}` and `%{time_total}` with real elapsed times, and an FTP upload's `%{size_request}`, as curl 8.22.0 does.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. FTP-8 and FTP-9: Curl prints `0.000000` for every timing (even after a 60 s wait) and `8 0 0 0` for `-T upload.txt -w "%{size_upload} %{size_download} %{size_request} %{size_header}"` where curl prints `8 0 8 0`. The FTP handler puts no timings on its `TransferReport`; see how `HttpProtocolHandler` fills them. If the change belongs in another FTP file, add it to `touches` by the lane rules.

## Acceptance criteria

- [ ] An FTP download prints non-zero, ordered times (connect <= pretransfer <= starttransfer <= total) and an 8-byte upload prints `8 0 8 0`, as real curl.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [ ] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 2e1a7764 d9aa5d84 40bddbd7 bc4b41f1.
