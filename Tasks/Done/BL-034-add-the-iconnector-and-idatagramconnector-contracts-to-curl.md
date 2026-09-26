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
completed: 2026-09-26
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

- [x] `IConnector`, `ConnectTarget`, `ConnectResult`, `IDatagramConnector`,
      `DatagramOpenResult`, `IDatagramChannel` and `DatagramReceived` exist in
      `Curl.Protocol.Abstractions.UnitLibrary`, one type per file, with exactly the
      members ADR-0005 names, and XML documentation on every public member stating the
      exit codes a failure carries and that only `OperationCanceledException` may escape
      an implementation.
- [x] `ConnectTarget` rejects an empty or whitespace host and a port outside 1-65535
      with `ArgumentException`/`ArgumentOutOfRangeException`; tests in
      `Curl.Protocol.Abstractions.UnitTests` cover both bounds (0, 1, 65535, 65536).
- [x] `ConnectResult.Failed` and `DatagramOpenResult.Failed` reject `CurlExitCode.Ok`
      with `ArgumentOutOfRangeException`, and `Connected`/`Opened` reject a `null`
      connection or channel; tests cover each, and assert that a success exposes its
      connection or channel with `ExitCode` `Ok` and a `null` message, and a failure the
      given code and message with a `null` connection or channel.
- [x] `ProtocolIsolationTests` still passes: the project references nothing.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Abstractions.UnitTests --filter "TestCategory!=Integration"`
      is green, with 100% line and branch coverage of the new types.

## Notes

- Delivered directly rather than through the full `/feature` stages: ADR-0005 already
  fixes every name and signature, so there was nothing for `protocol-architect` to plan.
- The ADR names the factories but not the result properties. Chose `Connection` /
  `Channel`, `ExitCode` and `ErrorMessage` (the last from the factories' `errorMessage`
  parameter), matching the acceptance criteria's wording.
- `ConnectResult` and `DatagramOpenResult` are sealed classes with a private
  constructor, not records like `FileOpenResult`: a record's public positional
  constructor and `with` would let a caller build a success without a connection,
  which the ADR's "created only through its factories" rules out.
- `ConnectTarget` is the positional record the ADR names; `Host` and `Port` are
  redeclared get-only with validating initializers, so `with` cannot bypass the checks.
  Parameter names in the exceptions are `Host` and `Port`. A `null` host throws
  `ArgumentNullException` (a subclass of `ArgumentException`).
- `DatagramReceived` is a `sealed record` class as the ADR writes it; a record struct
  would avoid an allocation per datagram, but TFTP's 512-byte blocks make that moot.
- Coverage (MSTest code-coverage collector, cobertura): `ConnectResult`,
  `ConnectTarget`, `DatagramOpenResult` and `DatagramReceived` all 100% line and
  branch. The interfaces have no executable code.
- Tests: `Curl.Protocol.Abstractions.UnitTests` 55 passing (was 32); solution fast
  tests 234 passing.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. IConnector, ConnectTarget, ConnectResult, IDatagramConnector, DatagramOpenResult, IDatagramChannel and DatagramReceived exist in Curl.Protocol.Abstractions, fully tested
