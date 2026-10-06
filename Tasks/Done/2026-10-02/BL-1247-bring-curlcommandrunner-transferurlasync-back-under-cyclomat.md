---
id: BL-1247
title: Bring CurlCommandRunner.TransferUrlAsync back under cyclomatic complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1247 — Bring CurlCommandRunner.TransferUrlAsync back under cyclomatic complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member: `CurlCommandRunner.TransferUrlAsync` is back under cyclomatic complexity 10.

## Context

- Found by BL-1191 on 2026-10-02: `CurlCommandRunner.TransferUrlAsync (async)` at `Curl.Console\CurlCommandRunner.cs:1971` measures cyclomatic complexity 12 (CRAP 12), over the gate of 10, with 100% line and branch coverage. BL-1191 did not touch the file; the last change to it was BL-1188's `[MULTI]` lines (5455dac3).
- The build's CA1502 does not catch it (the async state machine is counted by the coverage tool), so only the measurement shows it. Extract a step into a private method; behaviour must not change.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass unchanged.

## Notes

- 2026-10-02: Extracted `TryResolveTransferUrl` (scheme guess plus `-T` URL resolution) and `RefuseMalformedSetopt` (`--interface` then `--ech` refusal) out of `TransferUrlAsync`; same calls in the same order, so behaviour is unchanged. `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members. Build clean with -warnaserror; fast tests green.

## Log

- 2026-10-02: Created.
- 2026-10-02: Renumbered from BL-1218, which the archived Done/2026-10-02_1625 task already holds.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. TransferUrlAsync is back under complexity 10; Curl.Console has 0 failing members
