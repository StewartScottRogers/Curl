---
id: BL-3970
title: CGR-107c1: SshTransferClock stamps -w timings on every SFTP and SCP result
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3966]
touches: [Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Protocol.Ssh.UnitLibrary/ReceivedDataReportingStream.cs, Curl.Protocol.Ssh.UnitTests/Cgr107TimingTests.cs, Curl.Protocol.Ssh.UnitTests/Cgr030Tests.cs, Curl.Protocol.Ssh.UnitTests/Cgr130Tests.cs, Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerAdversarialTests.cs, Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.cs, Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.Compression.cs, Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.ConnectionReset.cs, Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.DiagnosticLog.cs, Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.KnownHostsWrite.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3970 — CGR-107c1: SshTransferClock stamps -w timings on every SFTP and SCP result

## Goal

SFTP and SCP results carry `TransferReport.Timings` (name lookup, connect, appconnect, pretransfer, starttransfer, total) and the received byte count, failed transfers included, so `-w` times and `%{speed_download}` are non-zero as curl 8.22.0 prints them.

## Context

Split from BL-3957 (itself split from BL-3636, CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). The work is done but was never committed; the shift stores it as a stash. Design from BL-3957's Notes: a private `SshTransferClock` in `SshProtocolHandler` stamps the timings on every result; name lookup and connect come from the connector, appconnect at "Authentication complete", pretransfer when the transfer starts, starttransfer at the first write to the output (new `ReceivedDataReportingStream.Written` hook), total when the handler returns. A failure before the first byte has no pretransfer. Existing SSH tests that compared whole `TransferResult`s compare `with { Report = null }`.

## Acceptance criteria

- [x] An sftp and an scp download result carries non-zero, ordered name-lookup, connect, appconnect, pretransfer, starttransfer and total times and the received byte count; a missing-file sftp result still carries non-zero connect and total times.
- [x] Focused tests in `Curl.Protocol.Ssh.UnitTests/Cgr107TimingTests.cs` fail before the fix and pass after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1` for `Curl.Protocol.Ssh.UnitLibrary` shows no member outside a gate.

## Notes

- Split from BL-3957 (over its cost cap).
- Continues the stashed work of BL-3957.
- 2026-10-28 (lane 12): the stashed work was complete; SSH unit tests 1934 passed, full fast suite green, Measure-CodeQuality for Curl.Protocol.Ssh.UnitLibrary shows 0 failing members. The tests pin a TimeProvider and in-memory server only, so they are platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 4a57893f 9e833f73 779790c0 b16f5955.
- 2026-10-28: Doing -> Done. SFTP and SCP results carry -w timings; tests, build and quality gate green
