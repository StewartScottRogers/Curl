---
id: BL-134
title: Add the transfer progress sink to ITransferContext in Curl.Protocol.Abstractions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-133]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-134 — Add the transfer progress sink to ITransferContext in Curl.Protocol.Abstractions

## Goal

`ITransferContext` exposes the progress sink BL-133's ADR decides, `TransferContext` defaults it to a do-nothing implementation, and every existing handler and test compiles and passes unchanged.

## Context

BL-133 recorded ADR-0045 (`Documentation/Planning/Decisions/ADR-0045-the-transfer-context-carries-a-progress-sink-for-the-progress-meter.md`), which names `ITransferProgress` (`ReportTransferStarted()`, `ReportDownloaded(long bytesSoFar, long? expectedTotal)`, `ReportUploaded(long bytesSoFar, long? expectedTotal)`), `NoTransferProgress.Instance`, and `ITransferContext.Progress`. Implement exactly what that ADR says; if it and this task disagree, the ADR wins. The purpose is to let `Curl.Console` print the meter after a transfer that failed past connect/open (BL-130), show curl 8.21.0's live status line (BL-131), and draw the `-#` bar (BL-132).

Where the code goes:

- The new interface and its do-nothing implementation: new files in `Curl.Protocol.Abstractions.UnitLibrary/`, namespace `Curl.Protocol.Abstractions`, with XML doc comments like the neighbouring `ITransferContext.cs`.
- The new member: `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`, and an `init` property on `TransferContext.cs` defaulting to the do-nothing instance (the pattern ADR-0008 used for `ConnectTimeout` and `MaxTime`).
- `ITransferContext.Progress` has no default interface implementation (ADR-0045). At BL-133's run (2026-09-26) only `TransferContext` implements `ITransferContext` (`Curl.Protocol.File.UnitTests/Fakes/FakeTransferContext.cs` no longer exists); if another implementer has appeared (grep `: ITransferContext`), it needs the member too. Copying the sink across redirect hops in `RedirectFollower.NextHop` is BL-310, not this task.

No handler reports anything yet; `file://` is BL-129. The sink does not read time. No package may be added.

## Acceptance criteria

- [x] The interface, members and default named in BL-133's ADR exist in `Curl.Protocol.Abstractions.UnitLibrary` with exactly those names.
- [x] A test in `Curl.Protocol.Abstractions.UnitTests/TransferContextTests.cs` pins that a `TransferContext` built with only `Url` and `Output` returns the do-nothing sink, and one pins that an initialised sink is returned as given.
- [x] A test in `Curl.Protocol.Abstractions.UnitTests` calls every member of the do-nothing sink and pins that none throws.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` and `dotnet build Curl.Protocol.File.UnitTests -warnaserror` are clean, and `dotnet test --filter "TestCategory!=Integration"` is green for the whole solution; no new test needs `TestCategory=Integration`.
- [x] Every new line and branch in `Curl.Protocol.Abstractions.UnitLibrary` is covered by `Curl.Protocol.Abstractions.UnitTests` (the solution's 100% line and branch gate).

## Notes

- Implemented exactly ADR-0045, mirroring the `ITransferEvents`/`NoTransferEvents` pattern (ADR-0046): `ITransferProgress` (`ReportTransferStarted`, `ReportDownloaded`, `ReportUploaded`), sealed `NoTransferProgress` with a private constructor and `Instance`, `ITransferContext.Progress` with no default implementation, and `TransferContext.Progress` as an `init` property defaulting to `NoTransferProgress.Instance`.
- `grep ": ITransferContext"` found only `TransferContext`, so no other implementer needed the member; `Curl.Protocol.File.UnitTests` needed no change.
- Tests: `TransferContextTests` pins the default and the pass-through (new `StubTransferProgress`, like `StubTransferEvents`); `NoTransferProgressTests.EveryMember_Called_DoesNotThrow` calls every member with and without an expected total. The new library lines (constructor via `Instance`, three empty members, the property getter) are all executed by these tests and have no branches.
- Verified 2026-09-27: both `-warnaserror` builds clean, `dotnet format --verify-no-changes` clean on both Abstractions projects, whole-solution fast tests green (Abstractions 383 passed).


## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ITransferContext.Progress exists, defaulting to NoTransferProgress.Instance per ADR-0045
