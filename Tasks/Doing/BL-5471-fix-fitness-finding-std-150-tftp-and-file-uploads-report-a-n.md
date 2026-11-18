---
id: BL-5471
title: Fix fitness finding STD-150: TFTP and FILE uploads report a non-zero %{time_connect} (TFTP) and a 0 %{time_posttransfer}; TFTP downloads stamp %{time_pretransfer} at the start
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5471 — Fix fitness finding STD-150: TFTP and FILE uploads report a non-zero %{time_connect} (TFTP) and a 0 %{time_posttransfer}; TFTP downloads stamp %{time_pretransfer} at the start

## Goal

STD-150 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: TFTP and FILE uploads report a non-zero %{time_connect} (TFTP) and a 0 %{time_posttransfer}; TFTP downloads stamp %{time_pretransfer} at the start. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** tftp://, file:// write-out, Windows
- **Repro:** a Python TFTP server on 127.0.0.1:44100 (RFC 1350 with blksize/tsize OACK) serving `big.bin` (300000 bytes) and storing uploads; a local `f.txt` (10 bytes). `W='c=%{time_connect} pre=%{time_pretransfer} post=%{time_posttransfer} st=%{time_starttransfer} tot=%{time_total}\n'`
  - `curl -sS -T f.txt -w "$W" tftp://127.0.0.1:44100/u.bin`
  - `curl -sS -T f.txt -w "$W" file:///C:/<dir>/u.bin`
  - `curl -sS -o got.bin -w "$W" tftp://127.0.0.1:44100/big.bin`
- **Expected (REF):** TFTP upload `c=0.000000 pre=0.001226 post=0.001227 st=0.000000 tot=0.001262`; FILE upload `c=0` with `post` ≈ `tot`; TFTP download `pre=0.053619 post=0.053619 st=0.002039 tot=0.053692` (pre ≈ total, st < pre).
- **Actual (PORT):** TFTP upload `c=0.001975 pre=0.001975 post=0.000000 st=0 tot=0.003252`; FILE upload `c=0.000001 … post=0.000000`; TFTP download `pre=0.001852 st=0.001852 post=tot`.
- **Likely location:** `Curl.Original.Protocol.Tftp.UnitLibrary/TftpTransferTimings.cs` `Stamp()` — the upload branch builds `ConnectTimings(started, channelReady, channelReady, null)` and passes null posttransfer; the FILE upload path in `Curl.Original.Protocol.File.UnitLibrary` does the same.
- **Acceptance check:** both uploads print `c=0.000000` and `post` > 0 ≈ `tot`; the TFTP download prints `pre` ≈ `tot`.

## Acceptance criteria

- [ ] both uploads print `c=0.000000` and `post` > 0 ≈ `tot`; the TFTP download prints `pre` ≈ `tot`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-150 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: e04a0b74 69116afe 3d836d5e.
