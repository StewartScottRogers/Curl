---
id: BL-306
title: Record the ADR that --resolve and --connect-to are kept verbatim by the parser and checked at transfer time
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-202]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-306 — Record the ADR that --resolve and --connect-to are kept verbatim by the parser and checked at transfer time

## Goal

An ADR in `Documentation/Planning/Decisions` records that `--resolve` and `--connect-to` are stored verbatim on `CommandLineOptions` and their syntax is checked only when a transfer starts.

## Context

- BL-202 decided this under Stewart's delegation but could not write the ADR: `Documentation/Planning/Decisions` was held by BL-133 in another lane. The decision, its measurements and its reasons are in BL-202's `Notes`.
- Code: `CommandLineOptions.ResolveEntries` / `ConnectToEntries` and their rows in `CommandLineOptionTable` (`Curl.Cli.UnitLibrary`); tests in `Curl.Cli.UnitTests/CommandLineResolveOptionTests.cs`. Transfer-time checking is BL-214 / BL-244.

## Acceptance criteria

- [ ] A new ADR, marked "Decided by Claude under Stewart's delegation", states the decision, the curl 8.21.0 measurements from BL-202's `Notes` (exit 49 `Could not parse CURLOPT_RESOLVE entry '<entry>'` at transfer time; no parse-time refusal besides a missing value and the `--no-` spellings) and the telnet-option precedent.
- [ ] `Documentation/Planning/Decisions/README.md` lists it.

## Notes

## Log

- 2026-09-26: Created.
