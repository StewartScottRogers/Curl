# ADR-1023: EngineComposition keeps one unit per partial file

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4172)

## Context

`Curl.Engine.UnitLibrary/EngineComposition.cs` held `CreateProtocolHandlers` beside every small
factory it calls: the extension wrappers (`ThroughExtension`, `ThroughTransferHooks`,
`ExtensionHandlersOver`), the expect-100 hooks, the platform's LDAP dialect and SSH algorithms, the
FTP handler and its data connector, and the connection numbering. Every task that changed any one of
them named the file in its `touches`, so five tasks (BL-4083, BL-4094 to BL-4097) waited on one
another.

## Decision

- `EngineComposition.cs` keeps only `CreateProtocolHandlers`: the list of handlers the engine
  registers, one handler per entry.
- Each other unit lives in its own partial file of the same class, named for what it builds:
  `EngineComposition.Extensions.cs`, `EngineComposition.ContinueWait.cs`,
  `EngineComposition.PlatformBuilds.cs`, `EngineComposition.Ftp.cs` and
  `EngineComposition.ConnectionNumbering.cs`, beside the existing `Authentication`, `Connectors`,
  `DebugBuild`, `Dns`, `Proxy`, `Traces` and `Transports` files.
- A task that changes one unit names that unit's file in its `touches`; only a task that adds a
  handler or a `CreateProtocolHandlers` parameter names `EngineComposition.cs`.

## Alternatives not taken

- Self-registering handlers (a module initializer per handler adding itself to a static list): a
  static mutable registry is the service locator the solution rules out, and every handler needs a
  different set of the method's services, so each would still need its own parameter.
- One options record in place of `CreateProtocolHandlers`' parameters, so a new hook would not touch
  the signature: it changes every caller in `Curl.Console`, `ManagedCurlEngine` and the tests, far
  beyond what serialised the board; a later task may take it when parameters keep being added.

## Consequences

The split moves code only; no behaviour changes. `task-board.ps1 hotspots` no longer counts tasks
for the helpers on `EngineComposition.cs`.
