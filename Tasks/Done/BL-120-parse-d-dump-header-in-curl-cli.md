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
completed: 2026-09-26
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

- [x] A test in `Curl.Cli.UnitTests` parses `-D hd.txt URL` and asserts the new property is `hd.txt`.
- [x] A test parses `--dump-header - URL` and asserts the property is `-`.
- [x] A test asserts the property is `null` when `-D` is not given.
- [x] `-D` with no argument is refused with curl 8.21.0's exit code and stderr lines for a missing option argument (the same path `-d` with no argument takes).
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: the change is one table row, one property and its tests, and the table's own `CommandLineOption.FileName` factory already gives curl's blank refusal, flag-like warning and missing-parameter refusal.
- Measured against the local curl 8.21.0 on 2026-09-26 (`curl <args> http://127.0.0.1:1/` and a `file://` download):
  - `-D` / `--dump-header` as the last argument: exit 2, `curl: option -D: requires parameter` (as typed) plus the try-help line, the same as `-d`.
  - `-D ''`: exit 2, `curl: option -D: blank argument where content is expected`, so the row is a `FileName` row (not `Value`, which would accept empty).
  - `-D -x`: accepted with `Warning: The filename argument '-x' looks like a flag.`; `-D -` gets no warning.
  - Repeated `-D`: the last one wins (`-D - -D hx.txt` writes only the file; `-D hx.txt -D -` writes only standard output and creates no file).
  - `--no-dump-header`: exit 2, not reversible.
- Property name `CommandLineOptions.DumpHeaderFile` (`string?`), as the task suggested; `-` is kept verbatim for BL-121 to interpret as standard output.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -D/--dump-header parsed into CommandLineOptions.DumpHeaderFile (verbatim, last wins, curl 8.21.0 refusals and warning)
