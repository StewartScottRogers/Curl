---
id: BL-5782
title: Answer the read-callback served-scheme check without building a throwaway dispatcher
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5782 — Answer the read-callback served-scheme check without building a throwaway dispatcher

## Goal

A perform whose read-callback post goes to an extension URL decides whether a handler serves that scheme without building a second `ProtocolDispatcher`, so the check claims no one-shot engine state and builds no throwaway transports.

## Context

Found by the shift-end review of 2026-11-22 (ADR-0710; silent-failure-hunter, with type-design-analyzer and code-reviewer reporting the cost).

- `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs:246` (BL-5770) passes `scheme => createDispatcher(options, PerformShared, null, null, rtspSession, WebSocketConnection.PerformConnection).Serves(scheme)` to `TransferSettingsOf`.
- `CreateDispatcher` (`Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:281`) calls `shared.FtpPwdStop?.TryClaim()`, a one-shot flag for the engine's first transfer. The check runs before the read callback is read (`ManagedEasyTransfer.Context.cs:287`), so when that read aborts (`ExtensionPostReadFailedException`) the flag is claimed by a perform that built no real dispatcher, and the engine's next FTP perform no longer stops before PWD.
- Each such perform also builds every handler, the transports, connection pools and a `TcpConnectionListener` and discards them undisposed; `Parsed.cs:168` then builds the real dispatcher. Any side effect added to `CreateDispatcher` later would run twice.
- Fix by asking the engine for the schemes it serves (computed once from the handler catalogue and extension handlers), or by reusing the one dispatcher built here.

## Acceptance criteria

- [ ] `ManagedEasyTransfer.Perform.cs` no longer calls `createDispatcher` to answer the served-scheme question.
- [ ] A unit test on an engine built with `stopsFirstFtpTransferBeforePwd: true` performs an extension-URL read-callback post whose callback aborts, then an FTP perform, and asserts the FTP perform still stops before PWD.
- [ ] `TransferSettingsOf_ReadCallbackOnASchemeNoHandlerServes_IsReadOnlyWhenAHandlerServesIt` and the other BL-5770 tests still pass.
- [ ] `dotnet build` is clean and the fast tests are green; `Curl.Engine.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-22: Created.
