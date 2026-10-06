---
id: BL-135
title: Correct three statements about URLs that no longer match the code
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Documentation/Product/Requirements.md, Tasks/Backlog/BL-010-decide-urls-system-uri-cannot-round-trip.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-135 — Correct three statements about URLs that no longer match the code

## Goal

Three written statements about URL handling that are false of the code as it is now are
corrected, so nobody reading them is misled; no behaviour changes.

## Context

Found while writing ADR-0010 for BL-128
(`Documentation/Planning/Decisions/ADR-0010-representing-urls-system-uri-cannot-round-trip.md`).
Each is a "say what it does" defect under the root `CLAUDE.md`:

1. `Tasks/Backlog/BL-010-decide-urls-system-uri-cannot-round-trip.md`, first acceptance
   criterion (around line 34), says `new Uri(...)` throws `UriFormatException`
   ("A Dos path must be rooted") for **both** `file://C:` and `file://ab:/x`. On .NET
   10.0.12, `file://C:` throws "A Dos path must be rooted" but `file://ab:/x` throws
   "The hostname could not be parsed" - as the comment at
   `Curl.Protocol.File.UnitTests/FileUrlPathTests.cs` (around line 714) already records;
   the "Dos path" message is confirmed for `file://C:` in
   `Curl.Protocol.File.UnitTests/FileProtocolHandlerTests.cs` (around line 2421).
2. `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` line 10 documents `Url` as
   "the URL being transferred, after any scheme rewriting has been applied". No scheme
   guessing or rewriting exists anywhere in `Curl.Console`, `Curl.Cli.UnitLibrary` or
   `Curl.Core.UnitLibrary` (a search for "rewrit" finds only the Windows `-o` file-name
   sanitizer and the progress meter). The URL reaches the handler as parsed by
   `System.Uri`.
3. `Documentation/Product/Requirements.md` line 37, FR-012, says "there is no option
   parser yet". `Curl.Cli.UnitLibrary/CommandLineParser.cs` exists; it does not know
   `--path-as-is`, so `curl --path-as-is <url>` is refused with
   `curl: option --path-as-is: is unknown` and exit 2 (see
   `CommandLineRefusal.UnknownOption`). The rest of that sentence - nothing on
   `ITransferContext` carries the flag, so a transfer always removes dot segments -
   remains true.

This is a `docs` task: edit only the XML doc comment, the requirement text and the BL-010
task body. Do not add `--path-as-is` to the parser or change `ITransferContext`'s members.
Editing BL-010 is limited to correcting the factual sentence; do not change its
assignee, dependencies, acceptance boxes' ticked state, or its `Log`, other than
appending one `Log` line recording the correction.

## Acceptance criteria

- [x] In `Tasks/Backlog/BL-010-decide-urls-system-uri-cannot-round-trip.md` (or wherever
      BL-010 sits when this runs, unless archived), the first acceptance criterion
      attributes "A Dos path must be rooted" to `file://C:` only and "The hostname could
      not be parsed" to `file://ab:/x`, both still `UriFormatException`, with the .NET
      version (10.0.12) named; a `Log` line dated the day of the edit records the correction.
- [x] The `<summary>` of `ITransferContext.Url` in
      `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` no longer mentions
      scheme rewriting and states what the property is now (the URL being transferred, as
      given on the command line and parsed into a `System.Uri`).
      `Select-String -Path Curl.Protocol.Abstractions.UnitLibrary\*.cs -Pattern "rewrit"`
      returns nothing.
- [x] FR-012 in `Documentation/Product/Requirements.md` no longer says there is no option
      parser; it states that `CommandLineParser` does not recognise `--path-as-is` and
      refuses it as unknown with exit 2, and keeps the statement that nothing on
      `ITransferContext` carries the flag. No other requirement row changes.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes with the same test count
      as before; `git diff --stat` shows no `.cs` file changed other than
      `ITransferContext.cs`, and within it only comment lines.

## Notes

- Done directly rather than through `align-and-document`: three one-sentence corrections, each fact re-checked in the code first (the two test comments, `CommandLineRefusal.UnknownOption`, no `path-as-is` in `Curl.Cli.UnitLibrary`).
- Test count unchanged by construction: the only `.cs` change is a doc comment. Fast run: 0 failed.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ITransferContext.Url, FR-012 and BL-010's first criterion now state what the code does
