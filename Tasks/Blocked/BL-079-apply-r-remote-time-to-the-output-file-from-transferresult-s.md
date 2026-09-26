---
id: BL-079
title: Apply -R/--remote-time to the output file from TransferResult.SourceLastWriteTimeUtc
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-019, BL-009, BL-068]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console]
requirement: FR-011
created: 2026-09-26
completed:
---
# BL-079 — Apply -R/--remote-time to the output file from TransferResult.SourceLastWriteTimeUtc

## Goal

When `-R`/`--remote-time` is given and the transfer succeeds, the output file's
modification time is set to `TransferResult.SourceLastWriteTimeUtc`.

## Context

BL-019 made the value reachable: `TransferResult.SourceLastWriteTimeUtc`
(`Curl.Protocol.Abstractions.UnitLibrary/TransferResult.cs`) carries the source's
modification time in whole seconds, `null` when unknown or on an upload. A handler
cannot apply it because it does not own `ITransferContext.Output`
(ADR-0003, amendment of 2026-09-26). Whoever opens the `-o` output file applies it.
`PhysicalFileSystem` (BL-009, `Curl.Core.UnitLibrary/FileSystem`) is the real-disk
file system. curl 8.21.0: `curl -R -o out.txt file:///C:/dir/hello.txt` leaves `out.txt`
with the source's modification time truncated to whole seconds
(<https://curl.se/docs/manpage.html#-R>). Check upstream for the stdout case (no file,
nothing applied) and a `-z` condition that is not met before pinning them.

## Acceptance criteria

- [ ] With `-R` and a successful transfer whose `SourceLastWriteTimeUtc` is not `null`,
      the output file's last-write time equals that value; a test asserts it.
- [ ] Without `-R`, the output file's last-write time is left alone; a test asserts it.
- [ ] A `null` `SourceLastWriteTimeUtc` or a failed transfer leaves the output file's
      time alone; a test asserts each.
- [ ] Output to stdout under `-R` sets nothing and does not fail.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Filed by BL-019 as its follow-up. `touches` is a best guess at where the output file is
opened; re-plan it if the command line layer puts that elsewhere.

2026-09-26, lane 2: blocked for re-planning, no code written. Findings:

- `-R`/`--remote-time` is not parsed. `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`
  has no row for it and `CommandLineOptions` has no property for it, so
  `CurlCommandRunner` has nothing to check. Adding the row (a `NegatableFlag`, since
  curl accepts `--no-remote-time`) needs `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`.
- The `-o` file is opened in `Curl.Console/CurlCommandRunner.cs`
  (`TransferToOutputFileAsync`, through `DeferredOutputFileStream`). The time should be
  applied there, after `output.CompleteAsync` has closed the file. The tests for this
  runner are in `Curl.Console.UnitTests`, which is also missing from `touches`.
- `IFileSystem` (Abstractions) can only open files, not set their times. To keep
  Abstractions out of this task, put a small setter interface in Core that
  `PhysicalFileSystem` implements, and inject it into `CurlCommandRunner` next to
  `outputFileSystem`.

Suggested `touches`: `[Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary,
Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Re-plan touches: -R/--remote-time is not parsed (needs Curl.Cli.UnitLibrary + Curl.Cli.UnitTests) and the runner's tests are in Curl.Console.UnitTests; none are in touches
