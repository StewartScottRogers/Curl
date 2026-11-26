---
id: BL-6037
title: Match curl for -Z -F missing file: check the form at the group's first URL and report xfer_id 0 and up for earlier groups
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6025]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelRun.cs, Curl.Console.UnitTests/Cgr175Tests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6037 — Match curl for -Z -F missing file: check the form at the group's first URL and report xfer_id 0 and up for earlier groups

## Goal

Under `-Z`, a `-F` file that cannot be opened is noticed while setting up the option group's first URL, whatever the batch size, and the `-w` variables of the earlier groups' transfers match curl 8.22.0.

## Context

Found by BL-6025 (STD-228). Two differences are left:

- `CurlCommandRunner.FormSetupFailureAsync` checks the form only when the group's **last** URL is held in the first
  batch. curl 8.22.0 fails at the group's **first** URL, so when the first URL is in the batch and the last is past it
  (`--parallel-max 1 URL1 --next -F f=@nofile.txt URL2 URL3`), Curl does not abandon the batch. Measure that command
  line with `Record-CurlExchange.ps1` first. Checking at the first URL also makes
  `ParallelRun`'s `reportsOnlyFirst` and its option-group counting unnecessary: the failing transfer is then the last
  one held, so every held transfer is reported. `Curl.Console/CLAUDE.md` says every held transfer is reported.
- `-w "%{conn_id} %{xfer_id}"` for `-Z URL1 --next -F f=@nofile.txt URL2`: curl 8.22.0 Schannel prints `-1 0` for
  URL1 and `-1 -1` for URL2; Curl prints `-1 -1` for both (measured 2026-11-26).

A related case to leave alone: with the failing group's first URL past the batch
(`-Z --parallel-max 1 URL1 URL2 --next -F f=@nofile.txt URL3`) real curl 8.22.0 completes URL1 and URL2 and then
hangs (killed after 600 s).

## Acceptance criteria

- [ ] `-Z --parallel-max 1 URL1 --next -F f=@nofile.txt URL2 URL3` gives real curl's stdout, stderr and exit code, and sends what it sends.
- [ ] `-w "%{conn_id} %{xfer_id}"` gives curl's values for the earlier groups' transfers and the failing one.
- [ ] A focused unit test in `Curl.Console.UnitTests` pins each behaviour.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
