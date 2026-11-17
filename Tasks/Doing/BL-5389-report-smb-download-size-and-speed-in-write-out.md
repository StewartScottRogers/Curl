---
id: BL-5389
title: Report SMB download size and speed in write-out
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smb.UnitLibrary/SmbProtocolHandler.cs, Curl.Original.Protocol.Smb.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5389 — Report SMB download size and speed in write-out

## Goal

An SMB download reports `%{size_download}`, `%{size_delivered}` and `%{speed_download}` as the bytes received and a non-zero speed, in `-w` and `%{json}`.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-096 (Low, Confirmed by two reviewers; Windows and Linux builds, 3+ runs: write-out values only, the file is written correctly). Regression from the STD-079 timings fix. Area: smb:// download, write-out and `%{json}`.

**Repro.** An SMB2 server with a share holding `big.bin` (100000 bytes); the review used impacket `smbserver.py -smb2support -port 46445 -username alice -password secret SHARE <dir>` in WSL.
- `curl -sS -u alice:secret -o o.bin -w "%{size_download} %{speed_download} %{time_total} %{size_request}\n" smb://127.0.0.1:46445/SHARE/big.bin`
- `curl -s -u alice:secret -o o.bin -w "%{json}" smb://127.0.0.1:46445/SHARE/big.bin`

**Expected (REF2; REF has SMB disabled).** `100000 <non-zero speed> <time> 711`; JSON has `"size_download":100000`.
**Actual (PORT).** `0 0 <time> 706`, exit 0, `o.bin` complete; JSON has `"size_download":0,"size_delivered":0,"speed_download":0`. Uploads report `size_upload` correctly.
**Where to start.** `Curl.Original.Protocol.Smb.UnitLibrary/SmbProtocolHandler.cs` `StampReport` (~lines 164-170): `(result.Report ?? new TransferReport()) with { Timings = timings, RequestSize = counting.BytesWritten }` builds a report without `DownloadSize`; set `DownloadSize = result.BytesTransferred` for downloads.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Smb.UnitTests` (a new file) prove a download's report has `DownloadSize` equal to the bytes transferred and, with an injected `TimeProvider`, a non-zero download speed.
- [ ] A test proves the report carries `size_download` and `size_delivered` of 100000 into `%{json}` for a 100000-byte download (through the write-out path where the SMB tests have a seam; otherwise on the `TransferReport` fields).
- [ ] A test proves an upload still reports `size_upload` and is unchanged (no regression from BL-5328).
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] Hand check recorded in the Log: the first command prints `100000` and a non-zero speed on Windows and Linux; `%{json}` shows `"size_download":100000`. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the first command prints `100000` and a non-zero speed on Windows and Linux; `%{json}` shows `"size_download":100000`.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 2270942a 2bec9e10 016fd271 7ae10ef7 392b1b2d a0b49a29.
