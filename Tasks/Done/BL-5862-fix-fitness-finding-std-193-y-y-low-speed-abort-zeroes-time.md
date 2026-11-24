---
id: BL-5862
title: Fix fitness finding STD-193: -Y/-y low-speed abort zeroes %{time_pretransfer}, %{time_starttransfer} and %{num_headers}
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/LowSpeedWatchdog.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5862 — Fix fitness finding STD-193: -Y/-y low-speed abort zeroes %{time_pretransfer}, %{time_starttransfer} and %{num_headers}

## Goal

STD-193 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: -Y/-y low-speed abort zeroes %{time_pretransfer}, %{time_starttransfer} and %{num_headers}. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Low · **Confidence:** Confirmed (Windows twice, Linux) · **Area:** HTTP write-out / `--speed-limit`.
- **Mock:** an HTTP server on 127.0.0.1:23001 where `/hold6` sends `HTTP/1.1 200` with `Content-Length: 10` and 3 body bytes, waits 6 s, then sends the remaining 7.
- **Repro:** `curl -s -o NUL -Y 5 -y 2 http://127.0.0.1:23001/hold6 -w "[%{time_starttransfer} %{time_pretransfer} %{num_headers} %{size_header} %{http_code} %{exitcode}]\n"`
- **Expected (REF):** `[0.003366 0.000943 1 39 200 28]` (non-zero timings, `num_headers` 1); stderr `curl: (28) Operation too slow. Less than 5 bytes/sec transferred the last 2 seconds`.
- **Actual (.NET):** `[0.000000 0.000000 0 39 200 28]`, same stderr. Also under `-Z` and through an HTTP proxy. With `-m 2` instead, the port matches REF.
- **Likely location:** `Curl.Core.UnitLibrary/LowSpeedWatchdog.cs` (~line 112): the watchdog's failure result does not carry the transfer's timings and header count as the `-m` deadline path does.
- **Acceptance check:** the repro prints non-zero `time_starttransfer` and `time_pretransfer` and `num_headers` 1.

## Acceptance criteria

- [x] the repro prints non-zero `time_starttransfer` and `time_pretransfer` and `num_headers` 1.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

The watchdog was not the cause: HttpProtocolHandler built its own report only for -m (EndedByLimit); a -Y/-y cancellation left with none, so the engine stamped a report with no pretransfer, starttransfer or headers. Fix: the HTTP handler puts exchange.Report on any other cancellation's Data, and EndPointReportingProtocolHandler keeps that report (adding end points). touches widened to those two files; no other Doing task holds them. Repro now prints [0.52 0.50 1 39 200 28]. Tests added in both .UnitTests projects; Engine, Http, Console fast tests green; Measure-CodeQuality 0 failing members for both libraries. Linux/macOS: change is platform-neutral.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: d29a6180 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. HTTP handler carries its report on a -Y/-y cancellation; engine keeps it
