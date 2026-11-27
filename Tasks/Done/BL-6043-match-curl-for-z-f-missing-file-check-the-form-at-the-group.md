---
id: BL-6043
title: Match curl for -Z -F missing file: check the form at the group's first URL and report xfer_id 0 and up for earlier groups
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6025]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelRun.cs, Curl.Console.UnitTests/Cgr175Tests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6043 — Match curl for -Z -F missing file: check the form at the group's first URL and report xfer_id 0 and up for earlier groups

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

- [x] `-Z --parallel-max 1 URL1 --next -F f=@nofile.txt URL2 URL3` gives real curl's stdout, stderr and exit code, and sends what it sends.
- [x] `-w "%{conn_id} %{xfer_id}"` gives curl's values for the earlier groups' transfers and the failing one.
- [x] A focused unit test in `Curl.Console.UnitTests` pins each behaviour.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 Schannel (2026-11-26, `Record-CurlExchange.ps1`), 12 command lines: the named one (with and
  without `-sS`), one or two earlier groups, one to three earlier URLs, `--fail-early`, `--parallel-max 2`, globs in
  the earlier and the failing group, and the failing group first (with `--parallel-max 1` too). curl sends nothing,
  reports every held transfer through the failing group's first, prints `conn_id` -1 for all, `xfer_id` -1 for the
  failing one alone and 0 upwards for the others, with an empty `%{scheme}` and each `%{filename_effective}` named.
  Curl's stdout, stderr, exit code and request bytes are identical to curl's for all 12.
- `-T` with a file that cannot be opened in a later group goes through the same report, and curl 8.22.0 prints the
  same ids for it, so the earlier transfers keep their `xfer_id` there too (measured, and pinned by a test).
- `ParallelRun`'s `reportsOnlyFirst` and its option-group counting are removed: the failing transfer is now the last
  one held. `Curl.Console/CLAUDE.md` ("each held transfer is reported") is now true as written.
- The runner remembers the failing transfer's state (`stateWhoseSetupAbandonedFirstBatch`) rather than adding a
  property to `RunningTransferState.cs`, which `touches` does not name.
- Left alone, as Context says: the failing group's first URL past the batch, where real curl hangs.
- No ADR: this matches measured curl, with no behaviour choice to make.
- Quality run for `Curl.Console`: 0 failing members. `dotnet build` clean. Fast tests: the one failure of the full
  pass was the new `-T` test's setup (the fake file system opens any path not marked unreadable); fixed, and
  `Curl.Console.UnitTests` then passed (3335 passed, 0 failed). Platform-neutral change and tests.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: bddf3c99 8fbaa4aa bad1a921 3d836d5e.
- 2026-11-26: Doing -> Done. -Z with a missing -F file is checked at the group's first URL and the earlier transfers keep their xfer_id, byte-identical to curl 8.22.0
