---
id: BL-101
title: Update the command-line wiki page for --no- negation
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-054]
touches: [Documentation/Wiki/Command-Line-Parsing.md, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-101 — Update the command-line wiki page for --no- negation

## Goal

The command-line wiki page and FR-046 say that `--no-<name>` turns off a negatable flag and that curl's other `--no-` spellings are refused, as the parser does since BL-054.

## Context

BL-054 implemented `--no-` negation in `Curl.Cli.UnitLibrary` (`CommandLineOption.NegatableFlag`, `CommandLineRefusal.CannotBeReversed`) but its `touches` did not include `Documentation/`, so two statements are now false:

- `Documentation/Wiki/Command-Line-Parsing.md`, "What the parser does not do": "`--no-` negation is not implemented: `--no-silent` is refused as unknown today (task BL-054)."
- `Documentation/Product/Requirements.md`, FR-046: "Accepting a `--no-` spelling of a negatable option (`--no-silent` is refused as unknown today) is an open gap (task BL-054)."

The measured curl 8.21.0 behaviour is recorded in the XML remarks of `CommandLineOptionTable` and in BL-054's Notes.

## Acceptance criteria

- [x] `Documentation/Wiki/Command-Line-Parsing.md` no longer says `--no-` negation is unimplemented, and describes which rows negate (`--silent`, `--show-error`, `--insecure`, `--tftp-no-options`) and the `cannot be reversed with a --no- prefix` refusal for the rest.
- [x] FR-046 in `Documentation/Product/Requirements.md` no longer calls `--no-` negation an open gap.

## Notes

- Written in-session rather than delegated to `align-and-document`: two false sentences
  in two named files, with the facts already measured in `CommandLineOptionTable`'s
  remarks. Default taken because the edit is smaller than the hand-off.
- Wiki: added the `NegatableFlag` factory row, a `--no-name` row to the argument table,
  a "`--no-` negation" subsection (negating rows, last spelling wins, attached value
  ignored, `cannot be reversed` refusal before a value is taken, unknown spellings,
  short letters never negated), and a `CannotBeReversed` row to the refusal table.
  "one of three factories" became "one of these factories".
- Seen, not fixed (outside this task's goal): the wiki's factory table still omits
  `CommandLineOption.FileName`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Command-Line-Parsing wiki and FR-046 describe --no- negation and the cannot-be-reversed refusal as the parser does since BL-054
