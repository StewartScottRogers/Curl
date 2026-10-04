---
id: BL-1357
title: Cover the two branch gaps Measure-CodeQuality reports in SshProtocolHandler's constructor and HandshakeAndTransferAsync
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1357 — Cover the two branch gaps Measure-CodeQuality reports in SshProtocolHandler's constructor and HandshakeAndTransferAsync

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports no failing member.

## Context

- Measured 2026-10-03 on Windows by BL-1327, whose change added no branch to either member:
  - `SshProtocolHandler..ctor(IConnector, IFileSystem, SshAlgorithmPreferences, Encoding)` (`SshProtocolHandler.cs:81`): branch 75%. The public constructor passes `OperatingSystem.IsWindows()` and the real `Environment`/agent pieces; probably the platform-picked branch or a `??` in the chained constructor. Cover it with a test, or, if the branch is one the platform picks, exclude it under ADR-0083 as the library already does for `SystemSshAgentConnector`.
  - `SshProtocolHandler.HandshakeAndTransferAsync (async)` (`SshProtocolHandler.cs:255`): branch 66.67%. Likely the `finally`'s awaited `DisconnectIgnoringFailureAsync` path when an exception escapes `AuthenticateAndTransferAsync`, or a cancellation path in the state machine. Read the coverage report's branch lines to find which.
- The measurement takes ~35 minutes on a loaded machine (the Ssh tests run under the collector).

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports no failing member.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Constructor gap: the public constructor converted `Environment.GetEnvironmentVariable` to a delegate twice; the compiler caches both conversions in one static field behind a null check, so the second null branch never ran. It now chains through a private constructor that converts it once.
- HandshakeAndTransferAsync gap: the `await` in the `finally` compiles to a rethrow that tests `obj is Exception`, whose false side no C# code reaches. The disconnect now runs after a `catch (Exception)` that captures an `ExceptionDispatchInfo` and rethrows it after the disconnect - same behaviour, no untakeable branch.
- Measured 2026-10-03: Curl.Protocol.Ssh.UnitLibrary 100% line, 100% branch, 0 failing members; Curl.Protocol.Ssh.UnitTests 1641 passed.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Curl.Protocol.Ssh.UnitLibrary measures 100% line and branch coverage with no failing member
