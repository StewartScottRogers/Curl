# ADR-1286: ITransferContext carries the no-proxy list as text (BL-5346)

Status: Accepted. Date: 2026-11-16. Decided by Claude under Stewart's delegation.

## Context

A handler that opens more than one connection (oci:// hops, BL-5345) must know whether a host is exempt from the transfer's proxy. `ITransferContext.Proxy` holds only the proxy chosen for the first URL. Extensions cannot reference `Curl.Core`, where `NoProxyMatcher` lives.

## Decision

`ITransferContext.NoProxy` is `string?`: the `--noproxy` text when given (even empty), else `no_proxy`, else `NO_PROXY`, else `null`, as `ProxySelector` reads it. `TransferContextFactory` sets it, reading the environment through an injectable `ReadEnvironmentVariable`. Every wrapper context forwards it. A `Func<string,bool>` test was rejected: the text is plain data that stays comparable in tests, and the consumer's own matcher stays free to log the matching entry.

## Consequences

Each `ITransferContext` implementation must forward `NoProxy`; the build flags one that does not.
