---
id: BL-496
title: Decide how a real curl option Curl does not implement yet is refused, apart from a typo
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-488, BL-489, BL-491, BL-492, BL-493, BL-494, BL-495]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-496 — Decide how a real curl option Curl does not implement yet is refused, apart from a typo

## Goal

An ADR states what Curl prints and exits for each class of curl 8.21.0 long option: parsed, deprecated with no function (BL-488), accepted with no effect (BL-489), refused by design on the reference build (ADR-0017's HTTP/2 and HTTP/3 options), real but not yet implemented, and not a curl option at all.

## Context

- Conformance audit 2026-09-28, row 33 (Major; frames the handling of rows 1 and 2): of 280 long options, 138 are parsed, 4 are refused by design, and 138 are unknown to the parser. Today a real-but-unparsed option and a typo both print `curl: option --X: is unknown` and exit 2, and nothing records that this is deliberate.
- The line is built in `Curl.Cli.UnitLibrary/CommandLineRefusal.cs` (`UnknownOption`) and chosen in `CommandLineParser.cs` (`ParseNegatedLong`, `ParseUnlistedLong`, `ParseShortBundle`). `CurlOptionAliasTable.cs` already lists every real name, so the parser can tell the two apart.
- Depends on BL-488, BL-489 and BL-491 to BL-495 so the ADR is written against the classes as they exist once those land, and names them as the examples.
- Standing rules (root `CLAUDE.md`, "Decisions"): match the platform's curl, simplest thing that stays a drop-in replacement. A script that passes a real option must not silently get different behaviour; weigh a distinct message against keeping curl's exact `is unknown` text, and say what `--help <option>` and `-K` config lines do for such an option.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (the number checked unused: the folder already has duplicates, see the ADR renumbering task), Status Accepted, marked "Decided by Claude under Stewart's delegation", with Context, Decision, Consequences and the alternatives weighed.
- [ ] The Decision states, for each class above, the exact standard-error line(s) and exit code, and for the not-yet-implemented class whether a config-file (`-K`) line is treated the same way.
- [ ] The ADR names `CommandLineRefusal.UnknownOption` and the alias table as where the classes are told apart, and states that the list of unimplemented names is derived from the two tables rather than written by hand.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
