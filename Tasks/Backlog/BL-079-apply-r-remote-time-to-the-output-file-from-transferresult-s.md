---
id: BL-079
title: Apply -R/--remote-time to the output file from TransferResult.SourceLastWriteTimeUtc
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-019, BL-009, BL-068, BL-125]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Product/Requirements.md]
requirement: FR-011
created: 2026-09-26
completed:
---
# BL-079 — Apply -R/--remote-time to the output file from TransferResult.SourceLastWriteTimeUtc

## Goal

When `-R`/`--remote-time` is given and the transfer succeeds, the `-o` output file's
modification time is set to `TransferResult.SourceLastWriteTimeUtc`.

## Context

- Depends on BL-125, which parses `-R`/`--remote-time`/`--no-remote-time` into
  `CommandLineOptions.RemoteTime` in `Curl.Cli.UnitLibrary`. This task does not change
  `Curl.Cli.UnitLibrary`.
- BL-019 made the value reachable: `TransferResult.SourceLastWriteTimeUtc`
  (`Curl.Protocol.Abstractions.UnitLibrary/TransferResult.cs`) carries the source's
  modification time in whole seconds, `null` when unknown or on an upload. A handler
  cannot apply it because it does not own `ITransferContext.Output` (ADR-0003, amendment
  of 2026-09-26). Whoever opens the `-o` output file applies it.
- The `-o` file is opened in `Curl.Console/CurlCommandRunner.cs`, method
  `TransferToOutputFileAsync`, through `DeferredOutputFileStream`. Apply the time there,
  after `output.CompleteAsync` has closed the file and only when the completed result
  succeeded.
- `IFileSystem` (Abstractions) can only open files, not set their times, and this task
  must not change Abstractions. Add a small interface in
  `Curl.Core.UnitLibrary/FileSystem/` (for example `IFileTimeSetter` with
  `SetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc)`), implement it on
  `PhysicalFileSystem` (BL-009) with `File.SetLastWriteTimeUtc`, and inject it into
  `CurlCommandRunner` beside `outputFileSystem`, registered where the runner is composed in
  `Curl.Console`. `Curl.Console.UnitTests/InMemoryFileSystem.cs` (or a new recording fake
  beside it) implements it for the runner tests.
- curl 8.21.0: `curl -R -o out.txt file:///C:/dir/hello.txt` leaves `out.txt` with the
  source's modification time truncated to whole seconds
  (<https://curl.se/docs/manpage.html#-R>; manpage checked 2026-09-26, documenting curl
  8.23.0). With no `-o`, output goes to stdout and there is no file to stamp.
- A `-z`/`--time-cond` condition that is not met writes no body; what curl 8.21.0 does to
  the `-o` file's time under `-R` in that case is not recorded. Measure it
  (`curl -R -z <future date> -o out.txt file:///...`, then read `out.txt`'s time, and
  whether `out.txt` exists) and match it.
- What curl prints when it cannot set the time is not measured and is out of scope; if
  the setter can fail, file a follow-up task rather than inventing a message.

## Acceptance criteria

- [ ] `Curl.Core.UnitTests` has a test that `PhysicalFileSystem`'s setter changes a real
      temporary file's last-write time to the given value (the file is deleted afterwards;
      no `TestCategory=Integration`).
- [ ] A test in `Curl.Console.UnitTests` asserts that with `-R` and a successful transfer
      whose `SourceLastWriteTimeUtc` is not `null`, the setter is called once with the
      `-o` path and that value, after the output file was completed.
- [ ] A test asserts that without `-R`, and with `-R --no-remote-time`, the setter is not
      called.
- [ ] Tests assert that a `null` `SourceLastWriteTimeUtc` and a failed transfer each leave
      the setter uncalled.
- [ ] A test asserts `-R` with output to stdout (no `-o`) calls nothing and exits 0.
- [ ] `Notes` records the measured curl 8.21.0 behaviour for `-R` with an unmet `-z`, and
      a test asserts the same.
- [ ] FR-011 in `Documentation/Product/Requirements.md` no longer says applying the time
      is an open gap; it states that `Curl.Console` applies it to the `-o` file after a
      successful transfer.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"`
      is green, and `Curl.Core.UnitLibrary` and `Curl.Console` keep 100% line and branch
      coverage.

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

Re-planned 2026-09-26: the parse is split out as BL-125 (`Curl.Cli.UnitLibrary` and its
tests), which this task now depends on; `touches` is now `Curl.Core.UnitLibrary`,
`Curl.Core.UnitTests`, `Curl.Console`, `Curl.Console.UnitTests` and
`Documentation/Product/Requirements.md` (for FR-011). The open `-z` question from the
original Context is now a measure-then-match criterion.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Re-plan touches: -R/--remote-time is not parsed (needs Curl.Cli.UnitLibrary + Curl.Cli.UnitTests) and the runner's tests are in Curl.Console.UnitTests; none are in touches
- 2026-09-26: Blocked -> Backlog. Re-planned: -R parse split out as BL-125 (now a dependency); touches now Core, Core.UnitTests, Console, Console.UnitTests and Requirements.md; criteria name the setter seam and the -z case to measure.
