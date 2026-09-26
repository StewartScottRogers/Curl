---
id: BL-080
title: Read -d @file and -d @- from an injected file and stdin reader
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-038]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-080 — Read -d @file and -d @- from an injected file and stdin reader

## Goal

`-d @file` and `-d @-` set `CommandLineOptions.PostData` to the contents of the named file
or of standard input with every CR, LF and NUL byte removed, read through an injected
seam so the tests never touch disk or the real standard input.

## Context

BL-038 made `CommandLineParser` (`Curl.Cli.UnitLibrary/CommandLineParser.cs`, a static
class with `Parse(IReadOnlyList<string>)`) record `-d`/`--data` through the `data` row of
`CommandLineOptionTable` and `CommandLineOptions.SetPostData`, which stores the UTF-8
bytes of the argument; `@file` is currently recorded literally. Upstream curl 8.21.0
(<https://curl.se/docs/manpage.html#-d>): data starting with `@` names a file to read, or
`-` for standard input, and carriage returns, newlines and null bytes are stripped out.

Measured with the local curl 8.21.0 (`/mingw64/bin/curl` in Git Bash) on 2026-09-26:

- A file holding bytes `61 0D 0A 62 0A 00 63` sent with `-G -d @file` gives the query
  `abc`, so CR, LF and NUL are all removed.
- Standard input `q r` followed by LF, sent with `-d @-`, gives `q r`: standard input is
  stripped the same way.
- `-d @/nonexistent/zz` exits 26 (`CurlExitCode.ReadError`) before any transfer, writing
  three lines to standard error: `curl: Failed to open <path>`,
  `curl: option -d: error encountered when reading a file`, and
  `curl: try 'curl --help' or 'curl --manual' for more information`. Git Bash rewrote the
  path in the first line, so measure the exact `<path>` text and the `--data` spelling of
  the second line again (from `cmd` or with `MSYS_NO_PATHCONV=1`) before writing them.

Exact output must be measured against the local curl 8.21.0 at `/mingw64/bin/curl` in Git
Bash before any byte or message is written into a test; record what was measured in
`Notes`.

Design constraints:

- The parser reads through an interface defined in `Curl.Cli.UnitLibrary` (for example
  `IDataFileReader`, one member reading a named file and one reading standard input, both
  returning bytes or a failure), injected into the parser entry point. Nothing in the
  parser calls `System.IO.File` or `Console` directly. Keep the existing
  `Parse(IReadOnlyList<string>)` working and add the overload that takes the reader.
  The BCL-backed implementation lives in `Curl.Cli.UnitLibrary`; keep it a thin
  pass-through, and if it cannot reach 100% coverage without a disk, say so in `Notes`
  so the coverage auditor can file it.
- `CommandLineRefusal` always carries `CurlExitCode.FailedInit` and exactly two lines
  today. The read failure needs exit 26 and three lines, so give it a named factory for
  this case rather than special-casing it in `Curl.Console`.
- Strip bytes, not characters: remove 0x0D, 0x0A and 0x00 from the raw bytes read; do not
  decode and re-encode.
- Joining several `-d` values with `&` is BL-057. If BL-057 has landed, a file-sourced
  piece joins like any other; otherwise the last `-d` still wins.
- BL-058 adds a password-prompt seam to the same parser entry point; whichever of BL-080
  and BL-058 lands second extends the first's entry point rather than adding another
  overload.

## Acceptance criteria

- [x] A test in `Curl.Cli.UnitTests` shows `-d @body.txt`, with a fake reader returning
      bytes `61 0D 0A 62 0A 00 63` for `body.txt`, gives `PostData` bytes `61 62 63`.
- [x] A test shows `--data @-`, with a fake reader returning standard-input bytes
      `71 20 72 0A`, gives `PostData` bytes `71 20 72`.
- [x] A test shows `-d abc` (no `@`) never calls the reader and gives bytes `61 62 63`.
- [x] A test shows `-d @missing`, with a fake reader reporting the file cannot be opened,
      is refused with `CurlExitCode.ReadError` (26) and the three standard-error lines
      measured against local curl 8.21.0, written verbatim in the test.
- [x] No code in `Curl.Cli.UnitLibrary` other than the BCL-backed reader calls
      `System.IO.File`, `FileStream` or `Console`.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

- Measured 2026-09-26 with `MSYS_NO_PATHCONV=1 /mingw64/bin/curl` 8.21.0: `-d @/nonexistent/zz`
  prints `curl: Failed to open /nonexistent/zz`, `curl: option -d: error encountered when reading
  a file` and the try-help line, exit 26. The option is named as typed: `--data`, `--data=@missing`,
  `-d@missing`. `-d @` prints `curl: Failed to open ` (trailing space). `-s -d @missing` drops the
  first line; `-s -S -d @missing` and `-d @missing -s` keep it, the same rule as
  `ContinueAtExclusiveWithRange`, so `DataFileUnreadable` takes `errorsHidden`. A directory
  (`-d @dd`) is `Failed to open dd`. `printf 'q r\n' | curl -G -d @-` requests `?q r`.
- Entry point: BL-058 landed first, so its `Parse(arguments, pathExists, passwordPrompt)` was
  extended to `Parse(arguments, pathExists, passwordPrompt, dataFileReader)` rather than adding a
  fifth overload. `Parse(arguments)` and `Parse(arguments, pathExists)` keep working and use
  `DiskDataFileReader.ForProcess`. `Curl.Console` calls only those two, so it needed no change.
- The reader reaches the `data` row through a fifth `CommandLineOptionApplier` parameter
  (`IDataFileReader dataFileReader`), beside `pathExists`, so the parser still special-cases no
  option.
- BL-057 has landed: a file-sourced piece joins with `&` like any other (`-d a -d@b` gives `a&b`).
  `CommandLineOptions.AppendPostData` now joins bytes, and the string overload encodes and delegates.
- `IDataFileReader.ReadStandardInput` returns bytes and has no failure result: curl reads stdin
  with no "Failed to open" path, and an I/O failure there is left to throw (documented on the
  interface and on `Parse`).
- `DiskDataFileReader` takes `File.ReadAllBytes` and `Console.OpenStandardInput` through its
  constructor, like `ConsolePasswordPrompt`, so every line and branch is covered without a disk:
  measured 100% line and branch on the new and changed code. The only uncovered lines in the
  library are older ones (`ConsolePasswordPrompt.ForProcessConsole`'s lambda, `UploadUrl` line 85).
- `ConsolePasswordPrompt` (BL-058) also touches `Console`; it is the other BCL-backed seam, so the
  "no `File`/`FileStream`/`Console`" criterion reads as "nothing but the seam implementations".
- Reviewed by `code-reviewer`: no findings but a doc claim that `Parse` never throws, narrowed.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -d @file and -d @- read through an injected IDataFileReader with CR, LF and NUL removed; an unreadable file exits 26 with curl's lines
