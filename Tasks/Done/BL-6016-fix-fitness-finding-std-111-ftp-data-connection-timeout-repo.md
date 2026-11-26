---
id: BL-6016
title: Fix fitness finding STD-111: FTP data-connection timeout reports %{num_headers} 0 and %{response_code} 000 (PARTLY FIXED)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpNumHeadersTests.cs, Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitTests/EndPointReportingProtocolHandlerTimingsTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6016 — Fix fitness finding STD-111: FTP data-connection timeout reports %{num_headers} 0 and %{response_code} 000 (PARTLY FIXED)

## Goal

STD-111 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: FTP data-connection timeout reports %{num_headers} 0 and %{response_code} 000 (PARTLY FIXED). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (2 runs) · **Area:** FTP write-out, Windows build.
- **What is fixed:** uploads, 426/short-transfer and CWD-denied paths now match REF.
- **Mock server:** an FTP server on `127.0.0.1:25030` whose every reply contains a colon; user `badip` gets a PASV reply pointing at `10.255.255.1` (unreachable) and EPSV is refused.
- **Repro:**
  - `curl -sS -m 8 --no-ftp-skip-pasv-ip -w "[%{num_headers}]\n" ftp://badip:p@127.0.0.1:25030/f.txt`
  - `curl -sS -m 8 --connect-timeout 3 --no-ftp-skip-pasv-ip -w "[%{num_headers}|%{response_code}]\n" ftp://badip:p@127.0.0.1:25030/f.txt`
- **Expected (REF):** `[4]` and `[4|227]`, both exit 28.
- **Actual (.NET):** `[0]` and `[0|000]`, both exit 28; stderr matches.
- **Likely location:** `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs`: the data-dial timeout result does not go through `PseudoHeadersOf`, and the last reply code is not kept.
- **Acceptance check:** the commands print `[4]` and `[4|227]`.

## Acceptance criteria

- [x] the commands print `[4]` and `[4|227]`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: `-m` passing during the data connect leaves `FtpSession.RunAsync` as an `OperationCanceledException`, so no
  report was built. The session now puts a report (last reply code, colon reply lines, entry path) on the exception
  under `Curl.Engine.TransferReport`, the way the HTTP handler does (BL-5862), and rethrows, so the engine's watchdog
  still words the failure. No new decision, so no ADR.
- `touches` widened: `EndPointReportingProtocolHandler` took any handler report on a cancellation to mean the handler
  had counted its connections, which only HTTP does, so `%{num_connects}` would have become 0. No other task in Doing
  named these files.
- `--connect-timeout 3` does not end the data dial in curl 8.22.0 or Curl; both commands end at `-m 8`.
- Measured 2026-11-26 with `Record-CurlExchange.ps1 -Ftp`, real curl 8.22.0 against Curl: with the four colon replies
  `220`, `230` (to `USER`), `500` (to `EPSV`) and `227`, and a `PWD` reply holding no colon, both print `[4]` and
  `[4|227]`, exit 28. With every reply holding a colon both print 6; the count is the colon reply lines read.
  `%{num_connects}` 1 and `%{ftp_entry_path}` `/` match too.
- Quality: `Measure-CodeQuality.ps1` for the FTP and Engine libraries, 0 failing members. Fast tests green.
- Left: `-v` closing lines and `%{size_header}` differ on this path (BL-6049).

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 8df5245c 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. FTP -m timeout during the data connect now reports num_headers and response_code as curl does
