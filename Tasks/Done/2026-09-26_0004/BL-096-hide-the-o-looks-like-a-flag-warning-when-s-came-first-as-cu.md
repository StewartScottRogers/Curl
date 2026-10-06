---
id: BL-096
title: Hide the -o looks-like-a-flag warning when -s came first, as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-037]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-096 — Hide the -o looks-like-a-flag warning when -s came first, as curl does

## Goal

`curl -s -o -x URL` prints no warning, as curl 8.21.0 does, while `curl -o -x -s URL`
still prints `Warning: The filename argument '-x' looks like a flag.`

## Context

Measured on 2026-09-26 against the local curl 8.21.0 during BL-013: `curl -s -o -x
file:///C:/rp13/f.txt` prints nothing on standard error. curl's warnings are hidden by a
`-s` read before them (a later `-s` does not hide them, and `-S` does not bring them back).
`CommandLineOption.FileName` adds the warning unconditionally. BL-013 added
`CommandLineOptions.AddWarningLinesUnlessSilent` for the `-r` warnings; use it here too.

## Acceptance criteria

- [x] `-s -o -x URL` and `-sS -o -x URL` give no warning line; `-o -x -s URL` gives the one line.
      Tests in `Curl.Cli.UnitTests` pin all three.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Found already delivered when the lane picked it up: commit 095378c ("feat(cli): drop
  parser warnings raised while -s is in effect") routes `CommandLineOption.FileName`'s
  warning through `CommandLineOptions.AddWarningLinesUnlessSilent` (now via
  `WarnWhenFileNameLooksLikeFlag`, shared with `--cacert` since 3c14963).
- The three cases are pinned in `Curl.Cli.UnitTests/CommandLineParserTests.cs`:
  `Parse_FlagLikeOutputFileAfterSilent_IsAcceptedWithoutWarning` (`-s`, `-sS`) and
  `Parse_FlagLikeOutputFileBeforeSilent_KeepsTheWarning`. Choice: no code change and no
  duplicate tests; the `feature` pipeline stages were skipped because nothing was left to
  build. Verified on 2026-09-26: build clean, fast tests green (Curl.Cli.UnitTests 538 passed).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -s before -o -x hides the looks-like-a-flag warning; -o -x -s keeps it (already delivered in 095378c, tests pin it)
