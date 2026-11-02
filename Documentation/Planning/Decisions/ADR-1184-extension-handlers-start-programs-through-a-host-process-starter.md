# ADR-1184: Extension handlers start programs through a process starter the host may give

- Status: Accepted
- Date: 2026-11-02
- Amends ADR-0999, ADR-1142, ADR-1074
- Decided by Claude under Stewart's delegation (BL-4463)

## Context

The extended-protocol conformance runs (BL-4462 and the tasks after it) put curl's command line through the
engine in process, and a unit test may not start a real process (ADR-0421). The Plugin handler
(`curl-proto-<scheme>`, ADR-1142) and the MCP client's `mcp+stdio://` transport (ADR-1074) each built their own
`Process` adapter inside `CreateHandler`, and the Plugin handler also read `PATH` and `PATHEXT` from the process
environment, so a host composing the engine could neither stand in for the programs nor choose which helpers exist.

## Decision

- `Curl.Protocol.Abstractions` holds `IExtensionProcessStarter` (start a program with arguments, giving an
  `IExtensionProcess` with standard input, standard output, an exit wait and `Kill`; read one environment
  variable of the environment programs are found and started in) and `IExtensionProcess`. Starting a program
  that cannot be started throws `IOException`.
- `ProtocolHandlerServices` carries it as an optional last member, `ProcessStarter`, `null` by default, so every
  existing caller compiles unchanged. `EngineComposition.CreateProtocolHandlers` takes an optional
  `extensionProcessStarter` and passes it on.
- `null` means "the real thing": each extension then keeps its own `Process` adapter
  (`SystemHelperProcessStarter`, `McpServerProcessStarter`, both excluded from coverage per ADR-0083) and the
  process environment. A real starter in `Curl.Protocol.Abstractions` was rejected: it would be a third copy of
  the same `Process` code in a contracts library, and the extensions would still need their own adapters.
- Each extension adapts the host's starter to its own interface in its own file
  (`ExtensionHelperProcessStarter`, `ExtensionMcpServerProcessStarter`), so neither references the other. The
  MCP adapter turns the `IOException` into the `InvalidOperationException` its session already reports as exit 7.
- The Plugin search reads `PATH` and `PATHEXT` through the host's starter when one is given. The directory
  listing stays real: a test may create helper files in a temporary directory it deletes (ADR-0421).
- The environment reader sits on the starter rather than in a member of its own because the two describe one
  thing, the environment the programs run in: a host standing in for programs chooses both together.

## Consequences

A host (the conformance harness) can run `curl-proto-*` and `mcp+stdio://` transfers against in-memory stand-ins
and its own `PATH`. Curl's own hosts give no starter, so their behaviour is unchanged.
