---
id: BL-034
title: Add the IConnector and IDatagramConnector contracts to Curl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-032]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-034 — Add the IConnector and IDatagramConnector contracts to Curl.Protocol.Abstractions

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` declares the connector contracts ADR-0005
records - `IConnector`, `ConnectTarget`, `ConnectResult`, `IDatagramConnector`,
`DatagramOpenResult`, `IDatagramChannel` and `DatagramReceived` - so wire-protocol
handlers can be written and tested against them.

## Context

ADR-0005
(`Documentation/Planning/Decisions/ADR-0005-protocol-handlers-acquire-transports-through-connectors.md`,
written by BL-032) fixes every name and signature; implement exactly what it states. If
it and this task disagree, the ADR wins and the disagreement goes in `Notes`.

Model the result types on `FileOpenResult` in the same project: a success factory and a
failure factory, so a caller cannot build a success without a connection or a failure
without an exit code. Failure exit codes are curl's (`CurlExitCode`, taken from
<https://curl.se/libcurl/c/libcurl-errors.html>, checked against curl 8.21.0); the two a
TCP connector will return first are 6 (`CouldntResolveHost`) and 7 (`CouldntConnect`).

Nothing implements these contracts yet: the production connectors are separate
`Curl.Networking.UnitLibrary` tasks, and each protocol's tests will supply their own
fakes.

## Acceptance criteria

- [ ] `IConnector`, `ConnectTarget`, `ConnectResult`, `IDatagramConnector`,
      `DatagramOpenResult`, `IDatagramChannel` and `DatagramReceived` exist in
      `Curl.Protocol.Abstractions.UnitLibrary`, one type per file, with exactly the
      members ADR-0005 names, and XML documentation on every public member stating the
      exit codes a failure carries and that only `OperationCanceledException` may escape
      an implementation.
- [ ] `ConnectTarget` rejects an empty or whitespace host and a port outside 1-65535
      with `ArgumentException`/`ArgumentOutOfRangeException`; tests in
      `Curl.Protocol.Abstractions.UnitTests` cover both bounds (0, 1, 65535, 65536).
- [ ] `ConnectResult.Failed` and `DatagramOpenResult.Failed` reject `CurlExitCode.Ok`
      with `ArgumentOutOfRangeException`, and `Connected`/`Opened` reject a `null`
      connection or channel; tests cover each, and assert that a success exposes its
      connection or channel with `ExitCode` `Ok` and a `null` message, and a failure the
      given code and message with a `null` connection or channel.
- [ ] `ProtocolIsolationTests` still passes: the project references nothing.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Abstractions.UnitTests --filter "TestCategory!=Integration"`
      is green, with 100% line and branch coverage of the new types.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
