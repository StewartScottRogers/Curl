---
id: BL-136
title: Create no -o file when an unmet -z/--time-cond writes no body
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-079]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: FR-009
created: 2026-09-26
completed:
---
# BL-136 — Create no -o file when an unmet -z/--time-cond writes no body

## Goal

A transfer whose `-z`/`--time-cond` condition is not met leaves no `-o` file behind (and
an existing one untouched), while a genuine zero-byte download still creates an empty
`-o` file.

## Context

- Measured on curl 8.21.0 (Windows, Schannel build, 2026-09-26):
  - `curl -R -z "1 Jan 2030" -o out2.txt file:///Z:/tmp/src.txt` (unmet, `out2.txt` did
    not exist) exits 0 and creates **no** `out2.txt`.
  - The same with an existing `out3.txt` containing `old` exits 0 and leaves its content
    `old` (under `-R` its mtime is set to the source's; that part is BL-079's and BL-139's).
  - `-z "1 Jan 2000"` (met) downloads normally.
- `Curl.Console/DeferredOutputFileStream.cs`, `CompleteAsync`, creates the `-o` file for
  any successful result when nothing was written (`createsEmptyFile = file is null && result.IsSuccess`).
  That is right for a zero-byte download and wrong for an unmet condition.
- `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs`, `DownloadFromAsync`, returns
  `TransferResult.Success(0)` when `MeetsTimeCondition` fails; the same value it returns
  for `-I`/`NoBody` and an empty body. `TransferResult`
  (`Curl.Protocol.Abstractions.UnitLibrary/TransferResult.cs`) carries nothing that tells
  the caller "the condition was not met, no body was delivered", so the handler has to say
  so through the contract. A shared-contract change is why this task touches
  `Curl.Protocol.Abstractions.UnitLibrary`; ADR-0003 under
  `Documentation/Planning/Decisions/` governs the result/context shape and gets a dated
  amendment line if the record gains a member.
- `-z` is not parsed yet: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` has no
  `time-cond` row and `CurlCommandRunner.CreateContext` does not set
  `TransferContext.TimeCondition`. BL-138 covers that. This task does not need it: the
  handler is tested with `TransferContext.TimeCondition` set directly, and the console is
  tested with a fake dispatcher/handler returning the new result.
- Upstream reference: https://curl.se/docs/manpage.html#-z (checked against curl 8.21.0).

## Acceptance criteria

- [ ] `TransferResult` (or a factory on it) can express a success whose time condition was
      not met, for example a `bool TimeConditionUnmet` member defaulting to `false` and a
      `TransferResult.TimeConditionNotMet(DateTimeOffset? sourceLastWriteTimeUtc)` factory;
      `Curl.Protocol.Abstractions.UnitTests` covers the new member and factory, including
      `IsSuccess` being `true` and `SourceLastWriteTimeUtc` being carried.
- [ ] `FileProtocolHandler.DownloadFromAsync` returns that result for an unmet condition;
      a test in `Curl.Protocol.File.UnitTests` asserts it for both `TimeConditionKind`
      values, and that the met-condition, `-I`/`NoBody` and empty-file paths do not set it.
- [ ] `DeferredOutputFileStream.CompleteAsync` does not create the file for that result; a
      test in `Curl.Console.UnitTests` named
      `RunAsync_UnmetTimeCondition_CreatesNoOutputFile` asserts the `InMemoryFileSystem`
      holds no `-o` file afterwards and the exit code is `CurlExitCode.Ok`.
- [ ] A test asserts an existing `-o` file keeps its content (`old`) for that result.
- [ ] The existing zero-byte-download test (successful transfer, nothing written, empty
      file created) still passes unchanged.
- [ ] Under `-R`, an unmet condition with an existing `-o` file still stamps it with the
      source time (the runner still calls `IFileTimeSetter.TrySetLastWriteTimeUtc`).
- [ ] `Notes` states whether FR-009 in `Documentation/Product/Requirements.md` needs new
      wording for "no `-o` file on an unmet condition"; that file is outside this task's
      `touches`, so `align-and-document` makes the edit.
- [ ] `dotnet build -warnaserror` is clean for each touched library;
      `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1`
      reports no new failing member in `Curl.Protocol.Abstractions`, `Curl.Protocol.File`
      or `Curl.Console`.

## Notes

- Parsing `-z` onto the context is BL-138, filed alongside this task; do not widen this one
  to include it.
- Other handlers that honour `TimeCondition` later (HTTP, FTP) should return the same
  result for an unmet condition.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
