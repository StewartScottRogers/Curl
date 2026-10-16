# ADR-0538 — The engine's transport factories read `EngineToolOptions` and the easy groups

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2356.

## Context

`EngineComposition`'s transport factories and `OptionGroupConnectionSettings.Of` still took
`CommandLineOptions` for six values, and the local-binding reader used `Curl.Cli`'s `InterfaceBinding`.
BL-2310 needs the engine free of `Curl.Cli`.

## Decision

- **ALPN and `-A` come from `EasyHttpOptions`.** `UseAlpn` is `SslEnableAlpn` (CURLOPT_SSL_ENABLE_ALPN) and
  the tunnel's user agent is `UserAgent`; `CreateProxyTunnelOptions` takes the HTTP group.
- **The curl tool's own settings are an engine-owned `EngineToolOptions`.** `-Z`, `--parallel-immediate` and
  `--ssl-sessions` have no `CURLOPT_*`; `-m`'s trace-line limit rides with them so the factories keep their
  parameter lists (`-m` itself still reaches transfers through `EasyTransferOptions`). `Curl.Console` fills it
  with `EasyOptionsFromCommandLine.ToolOptionsOf`.
- **`--interface` is parsed by the engine's own `EngineInterfaceBinding`**, a copy of `Curl.Cli`'s
  `InterfaceBinding` with the same rules and tests; `Curl.Cli` keeps its own for the command line parser.
- **Parameters nothing read are dropped:** `CreateDnsResolver`, `CreateDohConnector` and
  `CreateDatagramConnectorQuicDialer` no longer take a command line at all.
