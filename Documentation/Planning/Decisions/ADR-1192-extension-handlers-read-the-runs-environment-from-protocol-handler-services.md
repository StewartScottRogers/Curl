# ADR-1192: Extension handlers read the run's environment from ProtocolHandlerServices

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4534
- Decided by Claude under Stewart's delegation.

## Context

`gs://` reads `STORAGE_EMULATOR_HOST`. `GcsExtension.CreateHandler` read it with
`Environment.GetEnvironmentVariable`, the process environment, so a run given an environment of its
own (`Curl.Console`'s in-process run in the conformance harness) never saw it and the conformance case
`GoogleCloudStorage:XMLAPI-Emulator-1` connected to storage.googleapis.com:443 and exited 7.
`ProtocolHandlerServices.ProcessStarter` carries an environment reader (ADR-1184), but no host sets
it, and setting one would also make the Plugin and MCP extensions start programs through it.

## Decision

`ProtocolHandlerServices` gains an optional `ReadEnvironmentVariable` (`null` by default: an extension
then reads the process environment). `ExtensionProtocolHandlers.For(readEnvironmentVariable)`, which
every host already calls with the run's reader, gives the extensions the services with that reader
set. `GcsExtension` reads `STORAGE_EMULATOR_HOST` through it, else the process environment. The
engine does not change.

## Consequences

Any extension that reads a variable can read the run's environment the same way. A host that calls
`ExtensionProtocolHandlers.Create` directly (`Curl.Api`, `Curl.Mcp`) still gives none, which is the
process environment those hosts run in.
