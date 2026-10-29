# ADR-1010: An extension connection hook replaces curl's connector, and two claiming one transfer fail

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4095)

## Context

Named pipes, vsock, MASQUE, SSH jump hosts and WireGuard each carry a transfer over a transport of
their own. Today only `--unix-socket` swaps the connector a transfer uses. The engine may not reference
an extension (`VerifyExtensionIsolation`), and with no extension option given curl's transports must be
exactly as before.

## Decision

- `Curl.Extension.Abstractions` (folder `Connection`) holds `IConnectionHook`: the `ExtensionOption`
  that activated it, `ConnectorOver(curlConnector)` and `DatagramConnectorOver(curlDatagramConnector)`,
  which gives curl's own unless the extension implements it. The hook receives curl's connector so a
  jump host or tunnel can reach its first hop through it.
- `ConnectionHooks.ActivatedBy` keeps the hook whose option the option group gave. A transfer runs over
  one transport, so when two or more extensions' options are given it throws an `ArgumentException`
  naming every option (`the options --a and --b each supply the connection a transfer runs over; give
  only one of them`); the host reports it as a command-line error. Hooks are not chained: no use named
  so far needs one transport stacked under another, and chaining would make the order of options matter.
- `EngineComposition.CreateProtocolHandlers` takes `extensionConnector` and `extensionDatagramConnector`
  (a host passes `ConnectionHooks.ConnectorOver` and `DatagramConnectorOver`) and applies them to its
  `connector` and `datagramConnector` first, so every handler, the endpoint recorder, the FTP data
  connector default and the extension handlers all see the extension's transport, just as they see the
  Unix-socket connector `--unix-socket` puts in place. `null` keeps curl's connectors untouched.

## Consequences

- An extension needs no engine change to carry transfers over its own transport.
- The Console wiring (passing the hooks from the parsed extension options) is a host task, as it was
  for the transfer hooks (ADR-1007).
