---
id: BL-126
title: Cover the compiler-generated record members in Curl.Protocol.Abstractions.UnitLibrary
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-126 — Cover the compiler-generated record members in Curl.Protocol.Abstractions.UnitLibrary

## Goal

`powershell -NoProfile -ExecutionPolicy Bypass -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` exits 0 again, because tests in `Curl.Protocol.Abstractions.UnitTests` exercise every compiler-generated record member it currently reports at 0% line coverage.

## Context

Found during BL-123 on 2026-09-26. The measurement above exits 1 with 15 failing members, all at 0% line coverage and all predating BL-123. They are compiler-generated record members that only a `with` expression reaches:

- Copy constructors: `ByteRange..ctor(ByteRange)` (`ByteRange.cs:27`), `TimeCondition..ctor(TimeCondition)` (`TimeCondition.cs:21`), `FileOpenResult..ctor(FileOpenResult)` (`FileOpenResult.cs:52`), `DatagramReceived..ctor(DatagramReceived)` (`DatagramReceived.cs:18`).
- Init setters: `FileOpenResult.set_Status`, `set_Content`, `set_Length`, `set_LastWriteTimeUtc` (`FileOpenResult.cs:53-56`); `TransferResult.set_ExitCode`, `set_BytesTransferred`, `set_ErrorMessage` (`TransferResult.cs:19-21`); `TimeCondition.set_Value`, `set_Kind` (`TimeCondition.cs:21`); `DatagramReceived.set_Length`, `set_RemoteEndPoint` (`DatagramReceived.cs:18`).

All files are in `Curl.Protocol.Abstractions.UnitLibrary`. Where to start: add one test per record to the existing test class for it (`ByteRangeTests.cs`, `TimeConditionTests.cs`, `FileOpenResultTests.cs`, `DatagramReceivedTests.cs`, `TransferResultTests.cs` in `Curl.Protocol.Abstractions.UnitTests`). Each test applies a `with` expression that sets every listed property and asserts the copy has the new values and the original keeps its old ones. Name each test for what it checks, e.g. `With_SettingEveryProperty_ReturnsCopyWithNewValuesAndLeavesOriginalUnchanged`.

Out of bounds: do not change the production library, do not add `[ExcludeFromCodeCoverage]`, and do not change any threshold in `CodeMetricsConfig.txt` or `Measure-CodeQuality.ps1`. If a member cannot be reached from a test without a production change, stop and file that as a separate task.

## Acceptance criteria

- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` exits 0, reporting 100% line and 100% branch coverage for the library.
- [ ] The only files changed are under `Curl.Protocol.Abstractions.UnitTests`; no `[ExcludeFromCodeCoverage]` is added and no threshold changes.
- [ ] Each of `ByteRange`, `TimeCondition`, `FileOpenResult`, `DatagramReceived` and `TransferResult` has a test that uses a `with` expression and asserts the copy's new values and the original's unchanged values.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitTests -warnaserror` is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes, and no new test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-09-26: Created.
