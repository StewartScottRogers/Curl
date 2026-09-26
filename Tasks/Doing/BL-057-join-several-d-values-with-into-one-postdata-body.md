---
id: BL-057
title: Join several -d values with & into one PostData body
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-038]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-057 — Join several -d values with & into one PostData body

## Goal

Every `-d`/`--data` on a command line contributes to one `CommandLineOptions.PostData`
body, the values joined in order with a single `&` byte (0x26), as upstream curl does.

## Context

BL-038 made `CommandLineOptions.SetPostData` (`Curl.Cli.UnitLibrary/CommandLineOptions.cs`)
replace any earlier value, so the last `-d` wins. Upstream curl 8.21.0
(<https://curl.se/docs/manpage.html#-d>): when these options are used more than once, the
data pieces are merged with a separating `&`, so `-d name=daniel -d skill=lousy` posts
`name=daniel&skill=lousy`.

Measured with the local curl 8.21.0 (`/mingw64/bin/curl` in Git Bash) on 2026-09-26, using
`-G` so the body shows in `%{url_effective}`: `-d @- -d '' -d y` with standard input `q r`
gives `q r&&y`, so an empty `-d` still contributes an empty piece and its separator.
Exact output must be measured against the local curl 8.21.0 at `/mingw64/bin/curl` in Git
Bash before any further byte value is written into a test.

Keep the change inside `CommandLineOptions` (append rather than replace) and the `data`
row of `CommandLineOptionTable`; the parser does not special-case an option (see
`Curl.Cli.UnitLibrary/CLAUDE.md`). Update the `PostData` and `SetPostData` XML doc
comments and any line in `Curl.Cli.UnitLibrary/README.md` that says the last `-d` wins.
`-d @file` reading is BL-080; if it has landed, a file-sourced piece joins like any other.

## Acceptance criteria

- [ ] A test in `Curl.Cli.UnitTests` shows `-d a -d b` gives `PostData` bytes `61 26 62`.
- [ ] A test shows `-d name=daniel --data skill=lousy` gives the UTF-8 bytes of
      `name=daniel&skill=lousy`.
- [ ] A test shows `-d a -d "" -d b` gives bytes `61 26 26 62`.
- [ ] A test shows a single `-d 75` still gives bytes `37 35`, and a command line with no
      `-d` leaves `PostData` null.
- [ ] No doc comment or README in `Curl.Cli.UnitLibrary` still says the last `-d` wins.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
