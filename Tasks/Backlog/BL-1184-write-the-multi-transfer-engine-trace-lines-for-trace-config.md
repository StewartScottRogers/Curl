---
id: BL-1184
title: Write the [MULTI] transfer engine trace lines for --trace-config multi, network and -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1159]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1184 — Write the [MULTI] transfer engine trace lines for --trace-config multi, network and -vvvv

## Goal

Curl writes curl 8.21.0's `[MULTI]` lines under `--trace-config multi`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1159 (ADR-0357 amendment). BL-1103 Notes hold the measured lines: `[MULTI] [INIT] added to multi, mid=1, running=1, total=2`, `pollset[]`, `multi_wait(...)`, state changes `[INIT] -> [SETUP]` ... `[COMPLETED] -> [MSGSENT]`, `[PGRS-*] set|added <n>ns`, `[CPOOL] added connection 0. The cache now contains 1 members`, `cf_setup_connect`, `xfer_setup: recv_idx=0, send_idx=0`, `multi_done: status: 0 prem: 0 done: 0`, `removed from multi, mid=1, running=0, total=1`. These come from curl's multi state machine, which Curl does not have as such: decide (ADR) which states Curl reports and where, and how the nanosecond stamps and poll lines are produced. Split again per line family with task-planner if one run cannot hold it.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (plain HTTP transfer, refused connect); stderr in Notes.
- [ ] Tests pin the stable `[MULTI]` lines of a plain HTTP transfer, and that none appears without `multi`, `network` or `all`; an ADR records how the volatile values are produced.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
