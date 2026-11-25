---
id: BL-5890
title: Fix fitness finding STD-209: -J with a Content-Disposition filename of conin$ exits 2 with ".NET" text "Cannot access a closed file."; curl exits 23
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineToolTransferOutputs.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5890 — Fix fitness finding STD-209: -J with a Content-Disposition filename of conin$ exits 2 with ".NET" text "Cannot access a closed file."; curl exits 23

## Goal

STD-209 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: -J with a Content-Disposition filename of conin$ exits 2 with ".NET" text "Cannot access a closed file."; curl exits 23. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (3 runs) · **Area:** HTTP `-O -J` output file handling, Windows build.
- **Mock:** an HTTP server whose `/cd/<urlsafe-base64>` answers 200 with `Content-Disposition: <decoded value>` and body `DATA\n`.
- **Repro** (in an empty directory): `curl -sS -OJ http://127.0.0.1:34000/cd/YXR0YWNobWVudDsgZmlsZW5hbWU9ImNvbmluJCI -w "[%{filename_effective} %{exitcode}]\n" < NUL` (the path encodes `attachment; filename="conin$"`).
- **Expected (REF 8.22.0):** stderr `curl: (23) Failed writing body`, stdout `[conin$ 23]`, exit 23.
- **Actual (.NET):** stderr `curl: (2) Cannot access a closed file.` (an ObjectDisposedException message), no `-w` output, exit 2. Plain `-o conin$` correctly exits 23.
- **Likely location:** the `-J` path in `Curl.Engine.UnitLibrary/EngineToolTransferOutputs.cs` (the remote-header-name stream wrapping): the stream is disposed after the open or write fails and is then used again.
- **Acceptance check:** the repro exits 23, prints `[conin$ 23]` on stdout, and stderr contains no .NET exception text.

## Acceptance criteria

- [ ] the repro exits 23, prints `[conin$ 23]` on stdout, and stderr contains no .NET exception text.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: c6a727e1 69116afe 3d836d5e.
