---
id: BL-035
title: Add TransferContext and the Phase 4 option members to ITransferContext
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-032]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-035 — Add TransferContext and the Phase 4 option members to ITransferContext

## Goal

`ITransferContext` carries the Phase 4 options ADR-0006 records, and a sealed
`TransferContext` data class in `Curl.Protocol.Abstractions.UnitLibrary` implements it,
so protocol tests build contexts without declaring their own `ITransferContext`.

## Context

ADR-0006
(`Documentation/Planning/Decisions/ADR-0006-transfer-context-carries-phase-4-protocol-options.md`,
written by BL-032) fixes the names; implement exactly what it states. The five new
members are `ReadOnlyMemory<byte>? PostData`, `System.Net.NetworkCredential? Credentials`,
`IReadOnlyList<string> TelnetOptions`, `int? TftpBlockSize` and `bool TftpNoOptions`. The
upstream behaviour behind each is quoted in ADR-0006 with its curl.se link and curl
version (8.21.0).

Adding interface members breaks every implementer. Today the only implementer is
`Curl.Protocol.File.UnitTests/Fakes/FakeTransferContext.cs`, which is why this task
touches that project; it gains the five members as settable properties defaulting to
"not given". `FileProtocolHandler` ignores all five - per ADR-0003 that is a legitimate
answer - and needs no change.

`TransferContext` has `init` properties; `Url` and `Output` are `required`;
`TimeProvider` defaults to `TimeProvider.System`; `Upload`, `ResumeFrom`, `Range`,
`TimeCondition`, `HeaderOutput`, `PostData`, `Credentials` and `TftpBlockSize` default to
`null`; `NoBody` and `TftpNoOptions` to `false`; `TelnetOptions` to an empty list;
`CancellationToken` to `CancellationToken.None`.

## Acceptance criteria

- [ ] `ITransferContext` declares the five members with XML documentation naming the
      curl option each mirrors, its "not given" value, and which scheme reads it.
- [ ] `TransferContext` exists in `Curl.Protocol.Abstractions.UnitLibrary` as a sealed
      class implementing `ITransferContext`, with the defaults listed in `Context`.
- [ ] A test in `Curl.Protocol.Abstractions.UnitTests` named
      `TransferContext_OnlyRequiredMembersSet_ReportsNotGivenForEveryOption` asserts
      every default listed in `Context`, and another asserts every member round-trips a
      value set with an object initializer.
- [ ] `FakeTransferContext` in `Curl.Protocol.File.UnitTests/Fakes` implements the five
      members as settable properties with the same defaults; no other file in
      `Curl.Protocol.File.UnitTests` changes, and every existing test there still passes.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green across the solution.

## Notes

`Curl.Protocol.File.UnitTests` keeps its own fake rather than switching to
`TransferContext`: migrating it is not needed for this task and would collide with the
`file://` tasks on the board that edit those tests.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
