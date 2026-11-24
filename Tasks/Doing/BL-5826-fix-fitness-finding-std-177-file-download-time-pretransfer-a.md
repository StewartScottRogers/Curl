---
id: BL-5826
title: Fix fitness finding STD-177: file:// download: %{time_pretransfer} and %{time_starttransfer} are stamped at the start, and %{time_posttransfer} differs from pretransfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5826 — Fix fitness finding STD-177: file:// download: %{time_pretransfer} and %{time_starttransfer} are stamped at the start, and %{time_posttransfer} differs from pretransfer

## Goal

STD-177 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: file:// download: %{time_pretransfer} and %{time_starttransfer} are stamped at the start, and %{time_posttransfer} differs from pretransfer. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** FILE write-out, Windows
- **Repro:** a 300,000-byte local file. `curl -sS -o NUL -w "pre=%{time_pretransfer} post=%{time_posttransfer} st=%{time_starttransfer} tot=%{time_total}\n" file:///C:/<dir>/big.bin`
- **Expected (REF):** e.g. `pre=0.000690 post=0.000690 st=0.000488 tot=0.000720` (`pre` = `post` ≈ `tot`, `st` < `pre`).
- **Actual (.NET):** e.g. `pre=0.000001 post=0.001211 st=0.000001 tot=0.001211`.
- **Likely location:** `Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs:213`, the download branch `new TransferTimings(started, …, resolved, completed, resolved, completed)` (BL-5387).
- **Acceptance check:** `pre` equals `post`, both ≈ `tot`, and `st` ≤ `pre`.

## Acceptance criteria

- [ ] `pre` equals `post`, both ≈ `tot`, and `st` ≤ `pre`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 317dbe7b 69116afe 3d836d5e.
