---
id: BL-938
title: Add IDiagnosticLog, DiagnosticLogLevel and NoDiagnosticLog to the abstractions and carry the log on ITransferContext and ConnectTarget
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-937]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-938 — Add IDiagnosticLog, DiagnosticLogLevel and NoDiagnosticLog to the abstractions and carry the log on ITransferContext and ConnectTarget

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` defines the diagnostic log contract every library can take (`IDiagnosticLog`, `DiagnosticLogLevel`, `NoDiagnosticLog`), and a transfer carries one on `ITransferContext.DiagnosticLog` and `ConnectTarget.DiagnosticLog`, defaulting to `NoDiagnosticLog.Instance` so nothing changes until a caller sets it.

## Context

- The design is BL-937's ADR (decisions 2, 3, 6, 8): read it first. This task builds only the contract; the writer is BL-917, the options BL-918, the wiring BL-919.
- Model it on the transfer event sink (ADR-0046): `ITransferEvents.cs`, `NoTransferEvents.cs`, `ITransferContext.Events` (line ~426 of `ITransferContext.cs`), `TransferContext.Events` (`TransferContext.cs` line ~132, an `init` property defaulting to `NoTransferEvents.Instance`) and `ConnectTarget.Events` (`ConnectTarget.cs` line ~70).
- `ITransferContext` is implemented by `TransferContext` and by wrappers in several libraries (`RedirectFollower` in Curl.Core, and handlers in Dict, Ftp, Imap, Ldap, Pop3, Rtsp, Smb, Smtp, Ws). Give `ITransferContext.DiagnosticLog` a default interface implementation returning `NoDiagnosticLog.Instance` so none of them has to change in this task; a wrapper that should forward the log is fixed in that library's instrumentation task (BL-920 to BL-929).
- The shape, as BL-937 decides it:
  - `public enum DiagnosticLogLevel { None = 0, Error = 1, Warning = 2, Info = 3, Verbose = 4 }`.
  - `public interface IDiagnosticLog { bool IsEnabled(DiagnosticLogLevel level); void Write(DiagnosticLogLevel level, string component, string message); }`: the message has no line end, the component is one of the ADR's fixed names.
  - `public sealed class NoDiagnosticLog : IDiagnosticLog` with a `static Instance`: `IsEnabled` is always `false`, `Write` does nothing.
  - `public static class DiagnosticLogComponents` with one `const string` per component name in the ADR (`Cli = "cli"`, `Runner = "runner"`, `Dns = "dns"`, …), so no caller spells a component by hand.
- Base class library only, native-AOT safe; no package.

## Acceptance criteria

- [x] `IDiagnosticLog.cs`, `DiagnosticLogLevel.cs`, `NoDiagnosticLog.cs` and `DiagnosticLogComponents.cs` exist in `Curl.Protocol.Abstractions.UnitLibrary` with XML doc comments citing the ADR, and `DiagnosticLogComponents` holds exactly the ADR's component names.
- [x] `ITransferContext.DiagnosticLog` exists with a default implementation returning `NoDiagnosticLog.Instance`; `TransferContext.DiagnosticLog` is an `init` property defaulting to `NoDiagnosticLog.Instance`; `ConnectTarget.DiagnosticLog` is an `init` property defaulting to `NoDiagnosticLog.Instance`.
- [x] `Curl.Protocol.Abstractions.UnitTests` has `NoDiagnosticLogTests` (`IsEnabled` false for every level, `Write` has no effect), and tests pinning the three defaults and that an assigned log is returned.
- [x] No other project needed a change to compile: `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built directly: a small, fully specified contract whose shape ADR-0222 (BL-937) already decided, so no separate architect plan.
- Only `TransferContext` implements `ITransferContext` today, so the default implementation of `ITransferContext.DiagnosticLog` is covered through a test-only `ForwardingTransferContext` that leaves it un-overridden, as a library's wrapper will until BL-920 to BL-929 forward it.
- `DiagnosticLogComponents` holds the ADR's 30 names; `NoDiagnosticLogTests.DiagnosticLogComponents_Names_AreExactlyTheAdrList` pins them by reflection, so a name added without the ADR list fails a test.
- `ConnectTarget` is a record, so `DiagnosticLog` joins its value equality just as `Events` already does.
- Verified: `dotnet build Curl.slnx -warnaserror` clean, fast tests green (Abstractions 622 passed), `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. IDiagnosticLog, DiagnosticLogLevel, NoDiagnosticLog and DiagnosticLogComponents exist, carried on ITransferContext, TransferContext and ConnectTarget, defaulting to NoDiagnosticLog.Instance
