---
id: BL-3968
title: Fix the -Z hold of BL-3699 so Curl.Console.UnitTests no longer hangs (CGR-170)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr170Tests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelRun.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3968 — Fix the -Z hold of BL-3699 so Curl.Console.UnitTests no longer hangs (CGR-170)

## Goal

Under `-Z`, a later URL that cannot be set up (bad glob, unreadable `-T` file) abandons the held first batch as curl 8.22.0 does (CGR-170), and `Curl.Console.UnitTests` passes without hanging.

## Context

Split from BL-3699 (over its cost cap). The ADR-0866 fix (hold the first `--parallel-max + 1` transfers in `ParallelRun`, open each `-T` file as held, abandon the batch on a setup failure) was written and stashed; the six `Cgr170Tests` pass alone, but `dotnet test Curl.Console.UnitTests --filter "FullyQualifiedName~Parallel" --blame-hang-timeout 1m` hangs after 95 tests. BL-3699's Notes (in Deferred) hold the measured curl behaviour and the likely cause.

## Acceptance criteria

- [ ] `Cgr170Tests` pass and pin the measured CGR-170 behaviour (an abandoned batch reports each held transfer with the setup failure's code; with `--parallel-max 1`, transfers 1 and 2 run, then `2 3 http://[::1` and `URL rejected: Bad IPv6 address` are reported).
- [ ] The `Parallel` tests in `Curl.Console.UnitTests` finish without hanging, and Notes names the cause of the hang.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3699.
- Continues the stashed work of BL-3699.
- Next step: list the `Parallel` tests that never finish (`--logger "console;verbosity=normal"`); likely a `-Z` test whose transfers must run at once is now held until the batch is complete. Perhaps hold only the setup checks, not the starts, or start held transfers when the URL loop next waits.
- Serial runs must take no changed path.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 56dcf0ab 4c4fc916 779790c0.
