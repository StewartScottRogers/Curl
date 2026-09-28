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
completed: 2026-09-28
---
# BL-496 — Decide how a real curl option Curl does not implement yet is refused, apart from a typo

## Goal

An ADR states what Curl prints and exits for each class of curl 8.21.0 long option: parsed, deprecated with no function (BL-488), real but not yet implemented (a temporary class: every such option has a task on the board that implements it), and not a curl option at all. There is no "refused by design" class: ADR-0017's HTTP/2 and HTTP/3 refusals are superseded (BL-655, BL-718) and those options are "not yet implemented" until BL-659 and BL-732 land.

## Context

- Conformance audit 2026-09-28, row 33 (Major; frames the handling of rows 1 and 2): of 280 long options, 138 are parsed, 4 are refused by design, and 138 are unknown to the parser. Today a real-but-unparsed option and a typo both print `curl: option --X: is unknown` and exit 2, and nothing records that this is deliberate.
- The line is built in `Curl.Cli.UnitLibrary/CommandLineRefusal.cs` (`UnknownOption`) and chosen in `CommandLineParser.cs` (`ParseNegatedLong`, `ParseUnlistedLong`, `ParseShortBundle`). `CurlOptionAliasTable.cs` already lists every real name, so the parser can tell the two apart.
- Depends on BL-488, BL-489 and BL-491 to BL-495 so the ADR is written against the classes as they exist once those land, and names them as the examples.
- Standing rules (root `CLAUDE.md`, "Decisions"): match the platform's curl, simplest thing that stays a drop-in replacement, and (Stewart, 2026-09-28) do what a complete reimplementation of curl needs: no option is left out, so the ADR describes an interim state, never a permanent refusal; if any official curl build supports an option, Curl supports it on every platform. A script that passes a real option must not silently get different behaviour; weigh a distinct message against keeping curl's exact `is unknown` text, and say what `--help <option>` and `-K` config lines do for such an option.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (the number checked unused: the folder already has duplicates, see the ADR renumbering task), Status Accepted, marked "Decided by Claude under Stewart's delegation", with Context, Decision, Consequences and the alternatives weighed.
- [x] The Decision states, for each class above, the exact standard-error line(s) and exit code, and for the not-yet-implemented class whether a config-file (`-K`) line is treated the same way.
- [x] The ADR names `CommandLineRefusal.UnknownOption` and the alias table as where the classes are told apart, and states that the list of unimplemented names is derived from the two tables rather than written by hand.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- Decided in ADR-0137 (0137 was the next unused number; the highest on this branch was 0136). A real-but-unimplemented option is refused with curl's own `the installed libcurl version does not support this`, exit 2 - the line a curl built without a feature prints, already used for ADR-0017's `UnsupportedFlag` rows - rather than `is unknown` (misleads: says typo) or new text no curl prints.
- Measured 2026-09-28 against the Windows system curl 8.21.0 (Schannel): `--http3`, a `-K` line `http3`, a `-K` line `bogus-opt`, `--no-http3` and `--expand-http3 x`, all exit 2; texts in the ADR's Context.
- The ADR's example letters were computed from the two tables: 93 alias-table names had no option-table row on 2026-09-28, with letters `-4`, `-6`, `-n`, `-B`.
- The parser change is filed as BL-779 (`Curl.Cli.UnitLibrary`, `Curl.Cli.UnitTests`), outside this docs task's `touches`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0137 decides the four option classes: a real curl option not yet implemented is refused with 'the installed libcurl version does not support this', exit 2, apart from a typo's 'is unknown'; parser change filed as BL-779
