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
completed:
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

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports no failing member.
- [ ] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-03: Created.
