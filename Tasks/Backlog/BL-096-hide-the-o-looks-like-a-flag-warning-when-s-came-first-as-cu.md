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
completed:
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

- [ ] `-s -o -x URL` and `-sS -o -x URL` give no warning line; `-o -x -s URL` gives the one line.
      Tests in `Curl.Cli.UnitTests` pin all three.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
