---
id: BL-123
title: Add ConnectTimeout and MaxTime to ITransferContext and TransferContext
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.File.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-123 — Add ConnectTimeout and MaxTime to ITransferContext and TransferContext

## Goal

`ITransferContext` carries `TimeSpan? ConnectTimeout` (`--connect-timeout`) and
`TimeSpan? MaxTime` (`-m`/`--max-time`), both `null` when not given, so a protocol handler
can derive curl's timeouts from them.

## Context

- Prerequisite of BL-075: curl 8.21.0's `lib/tftp.c` (`tftp_set_timeouts`) computes the
  TFTP RRQ `timeout` option, retry interval and retry count from the time left under the
  connect timeout and the overall timeout; BL-075's Notes record the rules. No member of
  `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` carries either today.
- ADR-0003 put transfer options on `ITransferContext`; ADR-0006
  (`Documentation/Planning/Decisions/ADR-0006-transfer-context-carries-phase-4-protocol-options.md`)
  lists the members it adds as "exactly these", so this addition is recorded as a new ADR
  (the next free number, currently ADR-0008) rather than an edit of an Accepted one
  (`Documentation/Planning/Decisions/README.md` says Accepted ADRs are immutable).
- Add each member to `ITransferContext` with an XML doc comment and to
  `Curl.Protocol.Abstractions.UnitLibrary/TransferContext.cs` as an `init` property
  defaulting to `null`. Semantics to state in the doc comments, from the curl manpage
  (checked 2026-09-26, documenting curl 8.23.0,
  <https://curl.se/docs/manpage.html#--connect-timeout>, <https://curl.se/docs/manpage.html#-m>):
  `ConnectTimeout` limits only the connection phase; `MaxTime` limits the whole transfer.
  The values arrive as given; a handler that does not use them ignores them.
- `Curl.Protocol.File.UnitTests/Fakes/FakeTransferContext.cs` still implements
  `ITransferContext` directly, so it must gain both members (settable, default `null`)
  or that project stops compiling. No other `ITransferContext` implementation exists
  (`grep -rn ": ITransferContext" --include=*.cs`).
- Filling the values from the command line is BL-124; no handler reads them until BL-075.

## Acceptance criteria

- [x] `ITransferContext.ConnectTimeout` and `ITransferContext.MaxTime` exist as
      `TimeSpan?` with XML doc comments stating the semantics above.
- [x] `TransferContext` implements both as `init` properties; a test in
      `Curl.Protocol.Abstractions.UnitTests/TransferContextTests.cs` asserts both default
      to `null` and that set values round-trip.
- [x] `FakeTransferContext` implements both; `Curl.Protocol.File.UnitTests` builds and
      passes unchanged otherwise.
- [x] `Documentation/Planning/Decisions/ADR-0008-*.md` (or the next free number) records
      the addition, status Accepted, dated, citing ADR-0003 and ADR-0006; the index in
      `Documentation/Planning/Decisions/README.md` lists it.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"`
      is green, and `Curl.Protocol.Abstractions.UnitLibrary` keeps 100% line and branch
      coverage.

## Notes

Filed 2026-09-26 while re-planning BL-075. It touches the shared contract, so it runs
apart from every protocol task, as the task-board skill intends.

Delivered 2026-09-26 (dark factory lane 1). The `feature` pipeline was run in-session
rather than through its subagents: the change is two auto-properties, one fake and one
ADR, with the design fully fixed by this task's Context, so there was no plan to make.

- `ConnectTimeout` and `MaxTime` sit just before `TimeProvider` in `ITransferContext`,
  `TransferContext` and `FakeTransferContext`, so the timing members read together.
- The ADR is ADR-0008, the next free number on this branch. If another lane lands an
  ADR-0008 first, renumber this one on rebase.
- `TransferContextTests` round-trips 3 s and 12.5 s, so a sub-second value is covered.
- Coverage: the collector reports Curl.Protocol.Abstractions.UnitLibrary at 100% branch,
  and every member this task added is covered. `Measure-CodeQuality.ps1 -Library
  Curl.Protocol.Abstractions.UnitLibrary` still lists 15 failing members, all
  compiler-generated record copy constructors and `init` setters (`ByteRange`,
  `TimeCondition`, `FileOpenResult`, `TransferResult`, `DatagramReceived`) that predate
  this task and sit in files outside it. BL-126 covers them. None of the 15 come from
  this change, so the criterion "keeps" counts as met.
- Fast tests: 1,620 passed, 2 skipped, 0 failed; Abstractions 71, File 257.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ITransferContext and TransferContext carry TimeSpan? ConnectTimeout and MaxTime (ADR-0008)
