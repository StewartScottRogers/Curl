---
id: BL-055
title: Document the command-line parser in the wiki, glossary and requirements
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-055 — Document the command-line parser in the wiki, glossary and requirements

## Goal

The command-line parser BL-037 created in `Curl.Cli.UnitLibrary` is described in a wiki
page, its terms are in the glossary, and its refusals are requirements.

## Context

BL-037 created `CommandLineParser.Parse`, which returns a `CommandLineParseResult`
(`IsAccepted`, `Options`, `Refusal`); `CommandLineOptionTable.Rows` of
`CommandLineOption` rows made with `Flag`, `Text` or `Value`; `CommandLineOptions`, the
model the rows fill in; `CommandLineRefusal`, whose factories build the two stderr lines
and exit 2 (`CurlExitCode.FailedInit`); and `CommandLineNumber.ParseNonNegative`.
`Curl.Cli.UnitLibrary/README.md` describes it from inside the project; nothing in
`Documentation/` does.

`Documentation/Wiki/` does not exist yet: this task creates the folder, `Home.md` and
`Glossary.md` if they are still missing when it runs (the root `CLAUDE.md` names
`Documentation/Wiki/Glossary.md` as the one place a concept's name is defined). The
`Documentation` shared project's `.projitems` globs `**\*.md`, so no project file changes.

The refusal texts, measured with the local curl 8.21.0 on 2026-09-26 and asserted in
`Curl.Cli.UnitTests`, are in `CommandLineRefusal`: `is unknown`, `requires parameter`,
`blank argument where content is expected`, `expected a proper numerical parameter`,
`expected a positive numerical parameter`, each as `curl: option <spelled>: <reason>`
followed by `curl: try 'curl --help' or 'curl --manual' for more information`.

Write only what the code does at the time this task runs. BL-074, BL-051, BL-078, BL-053 and BL-054 extend the
parser; describe their behaviour only if they are in `Done` by then, and otherwise
mention them only as open gaps with their task IDs, in the same style as FR-006.
BL-033 (in Doing when this was filed) also adds requirements, so take the next free
`FR-` IDs at the time of editing, not a guessed number.

## Acceptance criteria

- [x] `Documentation/Wiki/Glossary.md` defines, each in one or two sentences true of the
      code: refusal; spelled option; option table and option row, as distinct from
      options (`CommandLineOptions`); flag option versus value option; bundle (`-sS`);
      try-help line; blank argument.
- [x] `Documentation/Wiki/Command-Line-Parsing.md` exists and states: the entry point
      and its result; how a row is added to the table; how short options, bundles,
      attached and separate values and `--url` are read; that the first refusal stops
      parsing; and that lines are returned without terminators for the console to write.
- [x] `Documentation/Wiki/Home.md` links to `Command-Line-Parsing.md` and `Glossary.md`.
- [x] `Documentation/Product/Requirements.md` has a `### The command line` section under
      `## Functional` with one requirement each for the unknown-option, requires-parameter,
      blank-argument and numeric (proper and positive) refusals, each giving the exact
      stderr lines, exit 2 (`CURLE_FAILED_INIT`), a link to
      <https://curl.se/docs/manpage.html> or
      <https://curl.se/libcurl/c/libcurl-errors.html>, and `curl 8.21.0`.
- [x] No statement in the new or edited documents describes behaviour the code in
      `Curl.Cli.UnitLibrary` does not have.

## Notes

- Created `Documentation/Wiki/Home.md`, `Glossary.md` and `Command-Line-Parsing.md`
  (the folder did not exist). Every statement was checked against the source of
  `Curl.Cli.UnitLibrary` and the refusal lines against `Curl.Cli.UnitTests` as of
  2026-09-26.
- Of the follow-ups, only BL-074 is in `Done` and in the code, so the no-URL refusal
  (`curl: (2) no URL specified`) is described as behaviour and given its own requirement,
  FR-051. BL-051, BL-053, BL-054 and BL-078 are in `Backlog` and appear only as open
  gaps with their IDs; so does BL-082 (zero arguments), found in BL-074's Notes.
- Requirements took FR-046 to FR-051 (FR-045 was the last ID when edited). The numeric
  refusal is split into FR-049 (proper) and FR-050 (positive) so each row holds one
  behaviour; `too large number` is already in FR-014 and was not repeated.
- The requirements header note now says the command line's refusals are authored and
  the rest of the command-line layer is not.
- The glossary also defines "applier" (`CommandLineOptionApplier`), because the wiki
  page and the other definitions use it.
- The wiki and FR section state that `Curl.Console` references the library but does not
  call the parser yet (no `CommandLine` use in `Curl.Console` source), so no document
  claims the executable refuses anything today.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Command-line parser documented: wiki page, glossary terms, FR-046 to FR-051 refusal requirements
