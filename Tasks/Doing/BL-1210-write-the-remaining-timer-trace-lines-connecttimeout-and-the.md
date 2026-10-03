---
id: BL-1210
title: Write the remaining [TIMER] trace lines: CONNECTTIMEOUT and the multi's expires-in lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1186]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1210 — Write the remaining [TIMER] trace lines: CONNECTTIMEOUT and the multi's expires-in lines

## Goal

Curl writes curl 8.21.0's `[TIMER] [CONNECTTIMEOUT]` lines, and the `[TIMER] ... expires in` lines that appear only when the multi is traced, under `--trace-config timer`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1186, which writes the `[TIMER] [HAPPY_EYEBALLS]` `set for`, `gives multi timeout in` and `cleared` lines from `ConnectAttemptTraceEvents`.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `curl -s --connect-timeout 1 http://localhost:1/`:
  - `-v --trace-config timer,multi`: `[TIMER] [CONNECTTIMEOUT] set for 1000000ns` first (`[0-x]` under `--trace-ids`), then `[HAPPY_EYEBALLS] set for 200000ns`, `[HAPPY_EYEBALLS] expires in 199989ns`, `[CONNECTTIMEOUT] expires in 999499ns`, `[HAPPY_EYEBALLS] gives multi timeout in 201ms`, later `[CONNECTTIMEOUT] expires in 792715ns` and `[CONNECTTIMEOUT] gives multi timeout in 793ms`.
  - `-vv --trace-config timer` and `-v --trace-config timer,tcp`: no `expires in` lines; `set` and `gives multi timeout` only. So `expires in` needs `multi` (which `network`, `all` and `-vvvv` include).
  - Values are volatile (ns elapsed, ms rounded up); decide how to write them under ADR-0357.
- Without `--connect-timeout` a plain 127.0.0.1 connect writes no CONNECTTIMEOUT line under `timer` (BL-1186 measurement); check `-m` and the default 300 s connect timeout before pinning.

## Acceptance criteria

- [ ] Tests pin the `[TIMER] [CONNECTTIMEOUT]` lines of a `--connect-timeout` connect and the `expires in` lines under `network`, and that `expires in` is absent under `timer` alone.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
