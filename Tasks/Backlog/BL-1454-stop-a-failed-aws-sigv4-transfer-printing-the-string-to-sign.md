---
id: BL-1454
title: Stop a failed --aws-sigv4 transfer printing the string to sign as its error message
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1454 — Stop a failed --aws-sigv4 transfer printing the string to sign as its error message

## Goal

A failed transfer under `--aws-sigv4` ends with curl's own error line (for a refused connection, `curl: (7) Failed to connect to <host> port <port> after <ms> ms: Could not connect to server`), and the SigV4 string to sign never reaches stderr without `-v`.

## Context

- Measured 2026-10-04 by the planner against curl 8.21.0 (Git for Windows mingw64, Schannel): with `--aws-sigv4` and a refused connection, Curl prints `curl: (7) aws_sigv4: String to sign ...` as the error line, where curl prints "Failed to connect". The string to sign leaks into the error output even without `-v`, and the transfer's real failure reason is lost.
- Likely cause: the SigV4 signer's -v line (BL-1227 added curl's `aws_sigv4: picked service/region from host` lines; BL-1244 printed them) is kept as the transfer's last message, and the error line uses the last message instead of the failure's. Find where `curl: (<code>) <text>` takes its text (Curl.Console's error writer) and where SigV4 records its lines (Curl.Authentication's AwsSigV4 signing result). The fix may need only one of the two libraries; narrow `touches` in Notes if so.
- curl 8.21.0 prints the string to sign only as a `-v` info line (`lib/http_aws_sigv4.c`), never as the error text.

## Acceptance criteria

- [ ] With `--aws-sigv4 aws:amz:us-east-1:s3` and a refused connection, Curl's stderr is curl's measured `curl: (7) Failed to connect to ...` line, byte for byte apart from the elapsed milliseconds, and contains no `String to sign` text (measure real curl with `Record-CurlExchange.ps1 -NoServer` and pin it).
- [ ] With `-v`, the SigV4 lines curl prints still appear as `*` info lines, in curl's order.
- [ ] A test pins that no SigV4 line becomes the error message for any failing exit code it reaches.
- [ ] `Measure-CodeQuality.ps1 -Library <every changed library>` in one run reports no failing member; `dotnet build` is clean and the fast tests are green.

## Notes
## Log

- 2026-10-04: Created.
