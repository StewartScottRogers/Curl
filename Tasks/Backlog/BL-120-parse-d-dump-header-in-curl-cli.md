---
id: BL-120
title: Parse -D/--dump-header in Curl.Cli
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-120 — Parse -D/--dump-header in Curl.Cli

## Goal

`CommandLineParser` accepts `-D <file>` and `--dump-header <file>` as curl 8.21.0 does and exposes the argument, exactly as given, on `CommandLineOptions`.

## Context

Found in BL-111 (2026-09-26): `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` has no `-D`/`--dump-header` entry, so `Curl.Console` cannot write header output or report its failure. `TransferContext.HeaderOutput` (`Curl.Protocol.Abstractions.UnitLibrary`) and the file handler already support header output; only the option is missing.

- Add a value option in `CommandLineOptionTable.cs` beside `data`/`-d`, storing the argument on a new `CommandLineOptions` property (e.g. `DumpHeaderFile`, `string?`). `-` means standard output; keep the text as given (curl prints it back verbatim in `curl: Failed writing headers to <file>`).
- Check against curl 8.21.0 what a repeated `-D` does (last one wins is curl's usual rule for a single-value option) and record it in Notes.
- Curl.Cli is held to 100% line and branch coverage and complexity at most 10 per method.

## Acceptance criteria

- [ ] A test in `Curl.Cli.UnitTests` parses `-D hd.txt URL` and asserts the new property is `hd.txt`.
- [ ] A test parses `--dump-header - URL` and asserts the property is `-`.
- [ ] A test asserts the property is `null` when `-D` is not given.
- [ ] `-D` with no argument is refused with curl 8.21.0's exit code and stderr lines for a missing option argument (the same path `-d` with no argument takes).
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
