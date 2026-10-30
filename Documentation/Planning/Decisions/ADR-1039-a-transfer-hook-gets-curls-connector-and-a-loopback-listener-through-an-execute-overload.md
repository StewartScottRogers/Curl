# ADR-1039: A transfer hook gets curl's connector and a loopback listener through an ExecuteAsync overload

- Status: Accepted
- Date: 2026-10-29
- Decision: Decided by Claude under Stewart's delegation (BL-4211)

## Context

An OAuth 2.0 extension (BL-4161, BL-4210) must fetch a token from a token endpoint, and for the
authorization code flow receive a redirect on a loopback port, before it sends the transfer with
`Authorization: Bearer <token>`. ADR-1007's `ITransferHook.RequestHeadersFor` is synchronous, and
ADR-1019's `ExecuteAsync(context, next)` gives a hook no connector or listener: the activators in
`ExtensionTransferHookCatalogue` are `Func<ExtensionOptionValues, ITransferHook?>` and see only the
option values, before any connector exists.

## Decision

- `TransferHookServices` (in `Curl.Extension.Abstractions.Transfer`) holds the option group's `IConnector`
  (the one curl's handlers connect through: TLS by `ConnectTarget.UseTls`, proxies as the target names)
  and an `IConnectionListener`.
- `ITransferHook` gains a default overload `ExecuteAsync(context, services, next)`, which calls the
  existing `ExecuteAsync(context, next)`. `TransferHookingProtocolHandler` calls this overload when the
  host gave services and the old one when it gave none, so every existing hook keeps its behaviour and
  every catalogue line keeps compiling.
- `TransferContextRequestHeaders.WithRequestHeaders(context, headers)` gives `next` a context whose HTTP
  headers are the context's own (curl's, then every hook's `RequestHeadersFor`) followed by the given
  ones; a transfer that is not HTTP is passed unchanged, as `RequestHeadersFor` is ignored for it.
- `TransferHooks` and `TransferHooks.ActivatedBy` take the services as an optional argument.
  `Curl.Console`'s `CurlComposition.ExtensionTransferHooksOf` passes the group's connector (the pooling
  connector, or the connector a test runner was given) and a `TcpConnectionListener` (the dialing runner's
  FTP listener when it was given one). The services are only reached when an extension option activated a
  hook, so a transfer without one is unchanged: the engine still gets no hook wrapper.

## Why

- Services go through `ExecuteAsync` rather than the activator: the activator runs before the group's
  connectors are built, and changing its type would break every catalogue line.
- An overload with a default body rather than a new parameter on the existing method keeps ADR-1019's
  hooks source compatible.
- A separate `TransferContextRequestHeaders` rather than a member of `TransferContextOutputs`, so each
  helper's name says what it replaces.
- The pooling connector rather than a fresh one: it is curl's connector for the group, with its proxy,
  TLS options and connection reuse, as the task asks.
