---
id: BL-662
title: Fail FTP with exit 11 for a weird PASS reply and exit 15 when the PASV host cannot be resolved
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-662 — Fail FTP with exit 11 for a weird PASS reply and exit 15 when the PASV host cannot be resolved

## Goal

The FTP handler ends with exit 11 (`CURLE_FTP_WEIRD_PASS_REPLY`) and curl 8.21.0's message when `PASS` gets a reply curl calls weird, and with exit 15 (`CURLE_FTP_CANT_GET_HOST`) and curl's message when the data-connection host cannot be resolved or connected, where today neither exit is produced.

## Context

- Conformance audit 2026-09-28, row 45 (Minor).
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`; `CurlExitCode.FtpWeirdPassReply` and `FtpCantGetHost` exist in Abstractions. Which PASS replies are "weird" (a `332` without `--ftp-account`? a `2xx` other than `230`? a `4xx`?) and what turns into 15 (with `--ftp-skip-pasv-ip` off and a `227` naming an unreachable address, or an `EPSV` reply on a host that fails) must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp -FtpReply`: `PASS` answered `202`, `332`, `421` and `530`; `PASV` answered with an unroutable address with and without `--ftp-skip-pasv-ip`; stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin each measured exit code and message.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-29, curl 8.21.0 (x86_64-w64-mingw32, Schannel), `Record-CurlExchange.ps1 -Ftp -FtpReply ... -CurlArgs -sS,-u,u:p,ftp://127.0.0.1:<port>/f.txt`:

| Case | Exit | stderr |
| --- | --- | --- |
| `PASS=202 Superfluous` | 0 | (none; logs in and downloads) |
| `PASS=231 Other` | 0 | (none) |
| `PASS=332 Need account` | 67 | `curl: (67) ACCT requested but none available` |
| `PASS=421 Bye` | 28 | `curl: (28) Timeout was reached` |
| `PASS=530 Login incorrect` | 67 | `curl: (67) Access denied: 530` |
| `PASS=150 Prelim` / `PASS=350 Intermediate` | 67 | `curl: (67) Access denied: 150` / `350` |
| `PASS=332`, `ACCT=530 No`, `--ftp-account acc` | 11 | `curl: (11) ACCT rejected by server: 530` |
| `PASS=332`, `ACCT=202 Superfl`, `--ftp-account acc` | 11 | `curl: (11) ACCT rejected by server: 202` |
| `PASS=332`, `ACCT=230 OK`, `--ftp-account acc` | 0 | (none) |
| `EPSV=500`, `PASV=227 (10,255,255,1,…)` | 0 | (none; address skipped by default) |
| same, `--no-ftp-skip-pasv-ip --connect-timeout 3` | 28 | `curl: (28) Failed to connect to 127.0.0.1:47705 via 10.255.255.1:56902 after 21066 ms: Could not connect to server` |
| same with `(0,0,0,0,…)` | 7 | `curl: (7) Failed to connect to 127.0.0.1:47706 via 0.0.0.0:58203 after 22 ms: Could not connect to server` |
| `PASV=227 (127,0,0,2,0,1)`, `--no-ftp-skip-pasv-ip` | 7 | `curl: (7) Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2041 ms: Could not connect to server` |
| `PASV=227 (127,0,0,1,0,1)`, default | 7 | `curl: (7) Failed to connect to 127.0.0.1:47708 via 127.0.0.1:1 after 2114 ms: Could not connect to server` |
| IPv6 `[::1]`, `EPSV=500`, `--no-ftp-skip-pasv-ip` | 8 | `curl: (8) Failed EPSV attempt, exiting` |
| IPv6 `[::1]`, `--disable-epsv`, PASV naming 127.0.0.1 (with and without `-6`) | 0 | (none) |

curl 8.21.0's binary holds `ACCT rejected by server: %03d` but no longer `cannot resolve new host`: the data host goes through the ordinary connect, so no case produces exit 15.

Decision (ADR-0216, decided by Claude under Stewart's delegation): no production change. Every measured `PASS` reply was already answered as curl does; exit 11 comes only from a refused `ACCT`, which needs `--ftp-account` in `ITransferContext` (Abstractions) and is BL-635's work, so BL-635 gained an acceptance criterion pinning it. Exit 15 is not produced, matching curl 8.21.0; data-connection failures keep the connector's exit (6, 7, 28). `FtpProtocolHandlerPassReplyTests` pins all of it.

`touches` gained `Documentation/Planning/Decisions` (for ADR-0216) and `Tasks/Backlog/BL-635-…` was edited; no task in Doing names either.

Follow-ups filed: BL-903 (IPv6 control with `EPSV` refused is exit 8, not a `PASV` fallback), BL-904 (a failed data connect names the control host `via` the data address).

Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Protocol.Ftp.UnitTests 380 passed); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. FTP PASS replies, ACCT exit 11 and PASV data-host failures pinned against curl 8.21.0; exit 15 is never produced, as in curl (ADR-0216)
