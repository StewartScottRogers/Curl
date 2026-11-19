---
id: BL-5476
title: Fix fitness finding STD-155: SMB timings: an upload reports a non-zero %{time_starttransfer}, and %{time_pretransfer} is stamped right after connect instead of near the end
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smb.UnitLibrary/SmbProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5476 — Fix fitness finding STD-155: SMB timings: an upload reports a non-zero %{time_starttransfer}, and %{time_pretransfer} is stamped right after connect instead of near the end

## Goal

STD-155 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: SMB timings: an upload reports a non-zero %{time_starttransfer}, and %{time_pretransfer} is stamped right after connect instead of near the end. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs, Windows and Linux builds)
- **Protocol/area:** smb:// write-out (compared with REF2; REF has SMB disabled)
- **Repro:** in Linux/WSL: `smbserver.py -smb2support -port 44445 -username alice -password secret SHARE ~/share` with `big.bin` (100000 bytes) in the share.
  - `curl -sS -u alice:secret -T h.txt -w "up=%{size_upload} c=%{time_connect} pre=%{time_pretransfer} st=%{time_starttransfer} tot=%{time_total}\n" smb://127.0.0.1:44445/SHARE/up3.txt`
  - `curl -sS -u alice:secret -o /dev/null -w "c=%{time_connect} pre=%{time_pretransfer} st=%{time_starttransfer} tot=%{time_total}\n" smb://127.0.0.1:44445/SHARE/big.bin`
- **Expected (REF2):** upload `up=5 c=0.000144 pre=0.004678 st=0.000000 tot=0.004681`; download `c=0.000087 pre=0.005367 st=0.003626 tot=0.005369` (pre ≈ tot).
- **Actual (PORT, Linux; Windows the same):** upload `up=5 c=0.001169 pre=0.001199 st=0.002752 tot=0.006641`; download `pre=0.001434 st=0.003018 tot=0.008202`.
- **Likely location:** `Curl.Original.Protocol.Smb.UnitLibrary/SmbProtocolHandler.cs` `StampReport`.
- **Acceptance check:** the upload prints `st=0.000000`; both print `pre` ≈ `tot`.

## Acceptance criteria

- [ ] the upload prints `st=0.000000`; both print `pre` ≈ `tot`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-155 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: e9e8eca4 69116afe 3d836d5e.
