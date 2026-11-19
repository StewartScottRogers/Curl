---
id: BL-5474
title: Fix fitness finding STD-153: FTP reports %{time_posttransfer} as 0 for downloads, uploads and -I
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5474 — Fix fitness finding STD-153: FTP reports %{time_posttransfer} as 0 for downloads, uploads and -I

## Goal

STD-153 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: FTP reports %{time_posttransfer} as 0 for downloads, uploads and -I. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** ftp:// write-out, Windows
- **Repro:** pyftpdlib on 127.0.0.1:44021, user `x`/`p`, serving `big.bin` (300000 bytes) and `a.txt`. `W2='pre=%{time_pretransfer} post=%{time_posttransfer} st=%{time_starttransfer} tot=%{time_total}\n'`
  - `curl -sS -o NUL -w "$W2" ftp://x:p@127.0.0.1:44021/big.bin`
  - `curl -sS -T a.txt -w "$W2" ftp://x:p@127.0.0.1:44021/up_t.txt`
  - `curl -sS -I -o NUL -w "$W2" ftp://x:p@127.0.0.1:44021/a.txt`
- **Expected (REF):** download `pre=0.003032 post=0.003032 st=0.003196 tot=0.003479`; `post` ≥ `pre` in all three.
- **Actual (PORT):** `post=0.000000` in all three; `pre`, `st`, `tot` non-zero.
- **Likely location:** `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs:403` `TimingsAt` passes null as posttransfer.
- **Acceptance check:** all three print `post` > 0 and ≥ `pre`.

## Acceptance criteria

- [x] all three print `post` > 0 and ≥ `pre`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

FtpSession.TimingsAt now passes connectPhaseEnded as RequestSent (posttransfer == pretransfer, matching the reference output in the finding). Unit tests in Cgr107TimingTests. Quality run: 0 failing members.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-153 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 02a0f095 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. posttransfer now equals pretransfer for FTP; unit tests added
