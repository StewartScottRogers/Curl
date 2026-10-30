# ADR-1014: Two extension connection-hook options are a command-line error with exit 2

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4192)

## Context

ADR-1010 lets one extension's `IConnectionHook` replace curl's connectors for an option group, and has
`ConnectionHooks.ActivatedBy` throw `ArgumentException` when two extensions' options claim the connection.
BL-4192 wires the hooks into `Curl.Console` and has to say how that conflict reaches the user. Upstream curl
has no extension options, so there is no curl output to measure; the nearest curl behaviour is an option
refused at transfer setup in `tool_operate` (exit 2, `CURLE_FAILED_INIT`, a `curl: ` line and the try-help line).

## Decision

- `Curl.Extension.Composition`'s `ExtensionConnectionHookCatalogue.Entries` lists each extension's
  connection-hook activator, one per line, `merge=union` and a shared list file, as
  `ExtensionTransferHookCatalogue` does (ADR-1007).
- `CurlComposition.ExtensionConnectionHooksOf` activates them for each option group's extension options and
  passes `ConnectorOver` / `DatagramConnectorOver` to `EngineComposition.CreateProtocolHandlers` as
  `extensionConnector` / `extensionDatagramConnector`, or `null` for both when no hook was activated, so a
  command line without an extension option builds exactly the connectors it built before.
- The `ArgumentException` becomes `ExtensionOptionConflictException` (the message without .NET's
  ` (Parameter 'values')` suffix). The runner catches it where it builds the group's dispatch and writes
  `curl: the options --a and --b each supply the connection a transfer runs over; give only one of them`
  and `curl: try 'curl --help' or 'curl --manual' for more information`, even under `-s`, and ends the run
  with exit 2 before the group transfers anything. Groups before it have already run, as curl runs the
  groups before a later group's setup refusal (test686).

## Consequences

The check runs when a group's dispatch is built rather than while parsing, because the parser does not know
which extensions supply connections; a later `--next` group with the conflict lets the earlier groups run.
