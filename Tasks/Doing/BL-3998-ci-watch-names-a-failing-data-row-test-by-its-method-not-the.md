---
id: BL-3998
title: CI watch names a failing data-row test by its method, not the row's display name
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-28
completed:
---
# BL-3998 — CI watch names a failing data-row test by its method, not the row's display name

## Goal

When a test with `[DataRow]`s fails in CI, the coordinator's CI watch files one task named after the test method, not one task per data row's display name.

## Context

`Get-CiFailures` (`DarkFactory/CiWatch.ps1:47`) takes the first word after `Failed` in `gh run view <id> --log-failed` as the test's name: `'^\s+Failed\s+(\S+)\s'`. For a data-row test, MSTest prints the row's display name there, not the method. On 2026-10-28, CI run 40774593052 logged

    Failed -4 http://[::1]:47500/ [1 ms]
    ...
     END Curl.Networking.TcpConnectorTests.ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral: Failed in 1 ms (...)
    Failed -6 http://127.0.0.1:47500/ [< 1 ms]

and the watch filed BL-3991 "Fix CI failure -4 on Linux and macOS" and BL-3992 "... -6 ...". Both were duplicates of BL-3993 and are deferred. Such a task names no test a lane can find, and the duplicate check (a test a live task already names is not filed again) cannot match it to the method's task.

The method's full name is in the same block, in the test logger's ` START <Namespace>.<Class>.<Method> (...)` and ` END <Namespace>.<Class>.<Method>: Failed` lines; MSTest's own TRX or `--logger` output also carries it. `DarkFactory/AffectedTests.ps1:36` reads `Failed <name> [` the same way and may want the same fix.

## Acceptance criteria

- [ ] For a failing data-row test, `Get-CiFailures` returns one item keyed by the method name (e.g. `ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral`), however many of its rows failed, on however many platforms.
- [ ] A test without data rows is still keyed by its name exactly as now (`Failed test1034: ...`, `Failed Parse_X_Y [..]`), and build errors and audit-guard lines are unchanged.
- [ ] The factory's CI watch self-test (`-TestCiWatch` or whichever `-Test*` covers `Get-CiFailures`) has a case built from the run 40774593052 lines above, and passes.
- [ ] The filed task's title and Context name the method, and its Context lists the failing rows' display names.

## Notes

## Log

- 2026-10-28: Created after BL-3991 and BL-3992 were filed for data rows "-4" and "-6".
- 2026-10-28: Backlog -> Doing. Criteria at claim: 83a704cc 187365af c02f9240 56ebb31d.
