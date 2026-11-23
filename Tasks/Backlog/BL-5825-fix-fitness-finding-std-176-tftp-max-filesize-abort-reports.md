---
id: BL-5825
title: Fix fitness finding STD-176: TFTP --max-filesize abort reports %{size_download} 0 although 1000 bytes were written
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Tftp.UnitLibrary/TftpDownload.cs, Curl.Original.Protocol.Tftp.UnitLibrary/TftpProtocolHandler.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5825 — Fix fitness finding STD-176: TFTP --max-filesize abort reports %{size_download} 0 although 1000 bytes were written

## Goal

STD-176 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: TFTP --max-filesize abort reports %{size_download} 0 although 1000 bytes were written. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** TFTP write-out, Windows
- **Repro:** a TFTP server on UDP 127.0.0.1:24069 serving `big.bin` (300,000 bytes) with a blksize/tsize OACK. `curl -sS -m 5 -o mf.bin --max-filesize 1000 -w "%{size_download}\n" tftp://127.0.0.1:24069/big.bin`
- **Expected (REF):** `curl: (63) Exceeded the maximum allowed file size (1000) with 1000 bytes`, then `1000`, exit 63; mf.bin is 1000 bytes.
- **Actual (.NET):** same message, exit 63 and file, but prints `0`.
- **Likely location:** `Curl.Original.Protocol.Tftp.UnitLibrary`, the max-filesize failure path builds the report without the bytes written.
- **Acceptance check:** the command prints `1000`, exit 63.

## Acceptance criteria

- [ ] the command prints `1000`, exit 63.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.