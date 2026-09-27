---
id: BL-312
title: Record the ADR that --resolve and --connect-to are kept verbatim by the parser and checked at transfer time
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-202]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-312 — Record the ADR that --resolve and --connect-to are kept verbatim by the parser and checked at transfer time

## Goal

An ADR in `Documentation/Planning/Decisions` records that `--resolve` and `--connect-to` are stored verbatim on `CommandLineOptions` and their syntax is checked only when a transfer starts.

## Context

- BL-202 decided this under Stewart's delegation but could not write the ADR: `Documentation/Planning/Decisions` was held by BL-133 in another lane. The decision, its measurements and its reasons are in BL-202's `Notes`.
- Code: `CommandLineOptions.ResolveEntries` / `ConnectToEntries` and their rows in `CommandLineOptionTable` (`Curl.Cli.UnitLibrary`); tests in `Curl.Cli.UnitTests/CommandLineResolveOptionTests.cs`. Transfer-time checking is BL-214 / BL-244.

## Acceptance criteria

- [x] A new ADR, marked "Decided by Claude under Stewart's delegation", states the decision, the curl 8.21.0 measurements from BL-202's `Notes` (exit 49 `Could not parse CURLOPT_RESOLVE entry '<entry>'` at transfer time; no parse-time refusal besides a missing value and the `--no-` spellings) and the telnet-option precedent.
- [x] `Documentation/Planning/Decisions/README.md` lists it.

## Notes

- Wrote ADR-0079 directly (small docs change, no delegation): decision, BL-202's curl 8.21.0 measurements, the `--telnet-option` precedent (BL-038/BL-044), and where the transfer-time check now lives (BL-214's `ResolveOverrides` / `ConnectToMappings`; BL-244 for `Curl.Console`). Number 0079 is the next free one after ADR-0078; if another lane takes 0079 first, the shift's rebase renumbers.
- No `.cs` or project file changed, so the `verify` skill was not needed for this docs task.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0079 records that --resolve and --connect-to are kept verbatim by the parser and checked at transfer time; README lists it
